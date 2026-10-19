using Erp.Application.Common;
using Erp.Application.Features.Crm.DTOs;
using Erp.Application.Features.Crm.Queries;
using Erp.Application.Features.Crm.Queries;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Erp.Application.Features.Crm.Commands;

/// <summary>
/// Executes <see cref="UpdateOpportunityCommand"/>: the commercial-dossier edit behind the
/// detail drawer. Every reference (item, territory, industry, currency) is re-resolved
/// against the tenant, lines replace in full, and <see cref="Opportunity.RecalculateTotals"/>
/// keeps the header money consistent. The EF RowVersion token guards the read/edit race
/// (spec CRM-06): a stale token fails with <c>concurrency_conflict</c>, zero writes.
/// </summary>
public sealed class UpdateOpportunityCommandHandler
    : ICommandHandler<UpdateOpportunityCommand, Result<OpportunityDetailDto>>
{
    private readonly ICrmRepository _crm;
    private readonly ICrmCatalogRepository _catalogs;
    private readonly IItemRepository _items;
    private readonly ICompanyRepository _companies;
    private readonly ICurrencyRepository _currencies;
    private readonly IExchangeRateRepository _exchangeRates;
    private readonly IQueryHandler<GetOpportunityDetailQuery, OpportunityDetailDto?> _detail;

    public UpdateOpportunityCommandHandler(
        ICrmRepository crm,
        ICrmCatalogRepository catalogs,
        IItemRepository items,
        ICompanyRepository companies,
        ICurrencyRepository currencies,
        IExchangeRateRepository exchangeRates,
        IQueryHandler<GetOpportunityDetailQuery, OpportunityDetailDto?> detail)
    {
        _crm = crm;
        _catalogs = catalogs;
        _items = items;
        _companies = companies;
        _currencies = currencies;
        _exchangeRates = exchangeRates;
        _detail = detail;
    }

    public async Task<Result<OpportunityDetailDto>> HandleAsync(
        UpdateOpportunityCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // One transaction for lines + header: a failure anywhere rolls the whole dossier
            // edit back (no header/line half-states). Joins nothing today (no ambient
            // transaction exists on this path) and composes if one ever wraps it.
            return await _crm.ExecuteInTransactionAsync(async token =>
            {
                var opportunity = await _crm.GetOpportunityByIdAsync(command.OpportunityId, token)
                    ?? throw new CRMValidationException(
                        "crm_opportunity_not_found",
                        $"Opportunity '{command.OpportunityId}' not found.");

                if (opportunity.CompanyId != command.CompanyId)
                {
                    throw new CRMValidationException(
                        "crm_opportunity_company_mismatch",
                        "Opportunity does not belong to the specified company.");
                }

                if (opportunity.RowVersion is null || !opportunity.RowVersion.AsSpan().SequenceEqual(command.RowVersion))
                {
                    throw new ConcurrencyConflictException(nameof(Opportunity), opportunity.Id);
                }

                if (opportunity.Status != OpportunityStatus.Open)
                {
                    throw new CRMValidationException(
                        CRMErrorCodes.OpportunityNotOpen,
                        $"Opportunity '{opportunity.OpportunityNumber}' is '{opportunity.Status}'; "
                        + "only open deals accept dossier edits.");
                }

                if (command.PartyName is not null)
                {
                    if (string.IsNullOrWhiteSpace(command.PartyName)
                        || command.PartyName.Trim().Length > OpportunityValidator.MaxPartyNameLength)
                    {
                        throw new CRMValidationException(
                            "crm_party_name_invalid",
                            $"Party Name is required and cannot exceed {OpportunityValidator.MaxPartyNameLength} characters.");
                    }

                    opportunity.PartyName = command.PartyName.Trim();
                }

                if (command.Probability.HasValue)
                {
                    if (command.Probability.Value < 0 || command.Probability.Value > 100)
                    {
                        throw new CRMValidationException(
                            CRMErrorCodes.InvalidProbabilityRange,
                            $"Probability must be between 0 and 100. Received {command.Probability.Value}.");
                    }

                    opportunity.Probability = command.Probability.Value;
                }

                if (command.ExpectedClosingDate.HasValue)
                {
                    opportunity.ExpectedClosingDate = command.ExpectedClosingDate.Value;
                }

                if (command.AssignedSalespersonId.HasValue)
                {
                    opportunity.AssignedSalespersonId = command.AssignedSalespersonId.Value == Guid.Empty
                        ? null
                        : command.AssignedSalespersonId.Value;
                }

                await ApplyCurrencyAsync(opportunity, command, token);
                await ApplySegmentationAsync(opportunity, command, token);

                if (command.Competitors is not null)
                {
                    opportunity.Competitors = command.Competitors
                        .Where(c => !string.IsNullOrWhiteSpace(c))
                        .Select(c => c.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }

                if (command.Items is not null)
                {
                    // The loaded Items nav is deliberately left untouched: line persistence
                    // goes through ReplaceOpportunityItemsAsync (explicit RemoveRange/AddRange),
                    // so no inferred graph state can misfire. Totals compute from the rebuilt
                    // list directly; the response re-reads fresh rows below.
                    var rebuilt = await RebuildItemsAsync(opportunity, command.Items, token);
                    opportunity.OpportunityAmount = RoundMoney(
                        rebuilt.Sum(i => i.Quantity * i.Rate * (1m - i.DiscountPercentage / 100m)));
                    opportunity.BaseOpportunityAmount = RoundMoney(
                        opportunity.OpportunityAmount * opportunity.ExchangeRate);
                    await _crm.ReplaceOpportunityItemsAsync(opportunity.Id, rebuilt, token);
                }
                else
                {
                    opportunity.RecalculateTotals();
                }

                await _crm.UpdateOpportunityAsync(opportunity, token);

                var detail = await _detail.HandleAsync(
                    new GetOpportunityDetailQuery(command.CompanyId, command.OpportunityId),
                    token);

                return Result<OpportunityDetailDto>.Success(detail!);
            }, cancellationToken);
        }
        catch (CRMValidationException ex)
        {
            return Result<OpportunityDetailDto>.Failure(ex.Code, ex.Message);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Result<OpportunityDetailDto>.Failure(ex.Code, ex.Message);
        }
    }

    private async Task ApplyCurrencyAsync(
        Opportunity opportunity,
        UpdateOpportunityCommand command,
        CancellationToken cancellationToken)
    {
        if (command.ClearCurrency)
        {
            opportunity.CurrencyId = null;
            opportunity.ExchangeRate = 1m;
            return;
        }

        if (!command.CurrencyId.HasValue && !command.ExchangeRate.HasValue)
        {
            return;
        }

        var currencyId = command.CurrencyId ?? opportunity.CurrencyId;
        if (currencyId.HasValue)
        {
            _ = await _currencies.GetByIdAsync(currencyId.Value, cancellationToken)
                ?? throw new CRMValidationException(
                    CurrencyErrorCodes.CurrencyNotFound,
                    $"Currency '{currencyId.Value}' was not found.");
        }

        decimal rate;
        if (command.ExchangeRate.HasValue)
        {
            if (command.ExchangeRate.Value <= 0m)
            {
                throw new CRMValidationException(
                    CRMErrorCodes.InvalidExchangeRate,
                    "Exchange rate must be positive.");
            }

            rate = command.ExchangeRate.Value;
        }
        else
        {
            rate = await ResolveRateAsync(opportunity, currencyId, cancellationToken);
        }

        opportunity.CurrencyId = currencyId;
        opportunity.ExchangeRate = rate;
    }

    /// <summary>
    /// Best-effort live rate (the ERPNext price-list-currency behavior): deal currency vs
    /// company currency at today, falling back to 1 when either side is unknown. An
    /// explicit rate from the caller always wins (see <see cref="ApplyCurrencyAsync"/>).
    /// </summary>
    private async Task<decimal> ResolveRateAsync(
        Opportunity opportunity,
        Guid? currencyId,
        CancellationToken cancellationToken)
    {
        if (!currencyId.HasValue)
        {
            return 1m;
        }

        var company = await _companies.GetByIdAsync(opportunity.CompanyId, cancellationToken);
        if (company?.CurrencyId is { } companyCurrencyId
            && companyCurrencyId != currencyId.Value)
        {
            var row = await _exchangeRates.GetLatestRateAsync(
                currencyId.Value, companyCurrencyId, DateOnly.FromDateTime(DateTime.UtcNow), cancellationToken);
            if (row is not null && row.Rate > 0m)
            {
                return row.Rate;
            }
        }

        return currencyId == opportunity.CurrencyId ? opportunity.ExchangeRate : 1m;
    }

    private async Task ApplySegmentationAsync(
        Opportunity opportunity,
        UpdateOpportunityCommand command,
        CancellationToken cancellationToken)
    {
        if (command.TerritoryId.HasValue)
        {
            if (command.TerritoryId.Value == Guid.Empty)
            {
                opportunity.TerritoryId = null;
            }
            else
            {
                var territories = await _catalogs.ListTerritoriesAsync(cancellationToken);
                if (!territories.Any(t => t.Id == command.TerritoryId.Value))
                {
                    throw new CRMValidationException(
                        CRMErrorCodes.TerritoryNotFound,
                        $"Territory '{command.TerritoryId.Value}' was not found.");
                }

                opportunity.TerritoryId = command.TerritoryId.Value;
            }
        }

        if (command.IndustryId.HasValue)
        {
            if (command.IndustryId.Value == Guid.Empty)
            {
                opportunity.IndustryId = null;
            }
            else
            {
                var industries = await _catalogs.ListIndustriesAsync(cancellationToken);
                if (!industries.Any(t => t.Id == command.IndustryId.Value))
                {
                    throw new CRMValidationException(
                        CRMErrorCodes.IndustryNotFound,
                        $"Industry '{command.IndustryId.Value}' was not found.");
                }

                opportunity.IndustryId = command.IndustryId.Value;
            }
        }

        if (command.MarketSegment is not null)
        {
            opportunity.MarketSegment = string.IsNullOrWhiteSpace(command.MarketSegment)
                ? null
                : command.MarketSegment.Trim();
        }
    }

    /// <summary>
    /// Validates the incoming lines and builds the replacement rows as a DETACHED list
    /// (never attached to the loaded nav): persistence goes through
    /// <see cref="ICrmRepository.ReplaceOpportunityItemsAsync"/>, and the header totals
    /// compute from this same list - the stale nav is irrelevant past this point because
    /// the response re-reads fresh rows.
    /// </summary>
    private async Task<IReadOnlyList<OpportunityItem>> RebuildItemsAsync(
        Opportunity opportunity,
        IReadOnlyList<OpportunityItemInput> inputs,
        CancellationToken cancellationToken)
    {
        var rebuilt = new List<OpportunityItem>(inputs.Count);
        foreach (var input in inputs)
        {
            if (input.Quantity <= 0m || input.Rate < 0m
                || input.DiscountPercentage < 0m || input.DiscountPercentage > 100m)
            {
                throw new CRMValidationException(
                    CRMErrorCodes.InvalidOpportunityItem,
                    "Opportunity lines need quantity > 0, rate >= 0 and discount between 0 and 100.");
            }

            var item = await _items.GetByIdAsync(input.ItemId, cancellationToken)
                ?? throw new CRMValidationException(
                    CRMErrorCodes.InvalidOpportunityItem,
                    $"Item '{input.ItemId}' was not found.");

            rebuilt.Add(new OpportunityItem
            {
                Id = Guid.NewGuid(),
                TenantId = opportunity.TenantId,
                OpportunityId = opportunity.Id,
                ItemId = item.Id,
                ItemName = item.ItemName,
                Quantity = input.Quantity,
                Rate = input.Rate,
                DiscountPercentage = input.DiscountPercentage,
            });
        }

        return rebuilt;
    }

    private static decimal RoundMoney(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
