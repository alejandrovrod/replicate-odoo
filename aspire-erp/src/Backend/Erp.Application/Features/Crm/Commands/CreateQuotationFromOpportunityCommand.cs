using Erp.Application.Common;
using Erp.Application.Features.Crm.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Erp.Application.Features.Crm.Commands;

/// <summary>One explicit quotation line (used only when the deal carries no priced lines).</summary>
public sealed record QuotationLineInput(Guid ItemId, decimal Quantity, decimal Rate);

/// <summary>
/// Issues a formal Draft quotation from a ClosedWon deal (CRM/ERPNext parity, the pre-order
/// flow): priced lines drag from the opportunity's own breakdown (or travel explicitly when
/// the deal is amount-only), money and numbering stay server-computed, and a Note activity
/// records the linkage (the SalesOrder conversion precedent - <see cref="Quotation"/>
/// carries the <c>OpportunityId</c> FK, so the history query needs no note parsing).
/// </summary>
public sealed record CreateQuotationFromOpportunityCommand(
    Guid OpportunityId,
    Guid CompanyId,
    DateOnly? ValidTill = null,
    string? TermsAndConditions = null,
    IReadOnlyList<QuotationLineInput>? Lines = null,
    Guid? CreatedByUserId = null
) : ICommand<Result<QuotationDto>>;

public sealed class CreateQuotationFromOpportunityCommandHandler
    : ICommandHandler<CreateQuotationFromOpportunityCommand, Result<QuotationDto>>
{
    private readonly ICrmRepository _crm;
    private readonly IQuotationRepository _quotations;
    private readonly ICrmActivityRepository _activities;
    private readonly ICustomerRepository _customers;
    private readonly IItemRepository _items;
    private readonly ICurrencyRepository _currencies;

    public CreateQuotationFromOpportunityCommandHandler(
        ICrmRepository crm,
        IQuotationRepository quotations,
        ICrmActivityRepository activities,
        ICustomerRepository customers,
        IItemRepository items,
        ICurrencyRepository currencies)
    {
        _crm = crm;
        _quotations = quotations;
        _activities = activities;
        _customers = customers;
        _items = items;
        _currencies = currencies;
    }

    public async Task<Result<QuotationDto>> HandleAsync(
        CreateQuotationFromOpportunityCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var opportunity = await _crm.GetOpportunityByIdAsync(
                    command.OpportunityId, cancellationToken)
                ?? throw new CRMValidationException(
                    "crm_opportunity_not_found",
                    $"Opportunity '{command.OpportunityId}' not found.");

            if (opportunity.CompanyId != command.CompanyId)
            {
                throw new CRMValidationException(
                    "crm_opportunity_company_mismatch",
                    "Opportunity does not belong to the specified company.");
            }

            if (opportunity.Status != OpportunityStatus.Won)
            {
                throw new CRMValidationException(
                    CRMErrorCodes.OpportunityNotWon,
                    $"Only a ClosedWon opportunity can issue a quotation; "
                    + $"'{opportunity.OpportunityNumber}' is '{opportunity.Status}'.");
            }

            var customer = await ResolveCustomerAsync(opportunity, cancellationToken)
                ?? throw new CRMValidationException(
                    CRMErrorCodes.OpportunityNoCustomer,
                    $"Opportunity '{opportunity.OpportunityNumber}' has no converted customer: "
                    + "convert its source lead first or link the deal to a customer.");

            if (customer.CompanyId != command.CompanyId)
            {
                throw new CRMValidationException(
                    "crm_opportunity_company_mismatch",
                    "Opportunity does not belong to the specified company.");
            }

            var lines = await ResolveLinesAsync(opportunity, command.Lines, cancellationToken);

            var currencyCode = "USD";
            if (opportunity.CurrencyId.HasValue)
            {
                currencyCode = (await _currencies.GetByIdAsync(
                    opportunity.CurrencyId.Value, cancellationToken))?.Code ?? "USD";
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var quotation = new Quotation
            {
                Id = Guid.NewGuid(),
                TenantId = opportunity.TenantId,
                CompanyId = command.CompanyId,
                QuotationNo = await _quotations.NextQuotationNumberAsync(
                    command.CompanyId, today.Year, cancellationToken),
                TransactionDate = today,
                ValidTill = command.ValidTill ?? today.AddDays(30),
                CustomerId = customer.Id,
                OpportunityId = opportunity.Id,
                Status = QuotationStatus.Draft,
                TotalAmount = Math.Round(lines.Sum(l => l.Quantity * l.Rate), 2, MidpointRounding.AwayFromZero),
                Currency = currencyCode,
                ExchangeRate = opportunity.ExchangeRate,
                TermsAndConditions = string.IsNullOrWhiteSpace(command.TermsAndConditions)
                    ? null
                    : command.TermsAndConditions.Trim(),
                CreatedAt = DateTimeOffset.UtcNow,
                Items = lines.Select(l => new QuotationItem
                {
                    Id = Guid.NewGuid(),
                    ItemId = l.ItemId,
                    Quantity = l.Quantity,
                    Rate = l.Rate,
                }).ToList(),
            };
            foreach (var item in quotation.Items)
            {
                item.QuotationId = quotation.Id;
            }

            await _quotations.AddAsync(quotation, cancellationToken);

            var authorId = command.CreatedByUserId ?? opportunity.AssignedSalespersonId;
            if (authorId.HasValue && authorId.Value != Guid.Empty)
            {
                await _activities.AddActivityAsync(new CRMActivity
                {
                    Id = Guid.NewGuid(),
                    OpportunityId = opportunity.Id,
                    Type = CRMActivityType.Note,
                    Subject = $"Quotation {quotation.QuotationNo} issued from this opportunity",
                    Content = $"Quotation {quotation.QuotationNo} issued from opportunity "
                        + $"{opportunity.OpportunityNumber} ({quotation.TotalAmount:0.00} {currencyCode}).",
                    ActivityDate = DateTimeOffset.UtcNow,
                    CreatedByUserId = authorId.Value,
                }, cancellationToken);
            }

            var names = await ResolveItemNamesAsync(
                quotation.Items.Select(i => i.ItemId).Distinct().ToList(), cancellationToken);

            return Result<QuotationDto>.Success(QuotationDto.Build(quotation, names));
        }
        catch (CRMValidationException ex)
        {
            return Result<QuotationDto>.Failure(ex.Code, ex.Message);
        }
    }

    /// <summary>
    /// Line source (the sales-order conversion precedent): the deal's own priced breakdown
    /// first, explicit caller lines for amount-only deals, rejection when neither exists.
    /// </summary>
    private async Task<IReadOnlyList<(Guid ItemId, decimal Quantity, decimal Rate)>> ResolveLinesAsync(
        Opportunity opportunity,
        IReadOnlyList<QuotationLineInput>? explicitLines,
        CancellationToken cancellationToken)
    {
        if (opportunity.Items.Count > 0)
        {
            return opportunity.Items
                .Select(i => (i.ItemId, Math.Abs(i.Quantity), i.Rate))
                .ToList();
        }

        if (explicitLines is not null && explicitLines.Count > 0)
        {
            var resolved = new List<(Guid, decimal, decimal)>(explicitLines.Count);
            foreach (var line in explicitLines)
            {
                if (line.Quantity <= 0m || line.Rate < 0m)
                {
                    throw new CRMValidationException(
                        CRMErrorCodes.InvalidOpportunityItem,
                        "Quotation lines need quantity > 0 and rate >= 0.");
                }

                _ = await _items.GetByIdAsync(line.ItemId, cancellationToken)
                    ?? throw new CRMValidationException(
                        CRMErrorCodes.InvalidOpportunityItem,
                        $"Item '{line.ItemId}' was not found.");

                resolved.Add((line.ItemId, line.Quantity, line.Rate));
            }

            return resolved;
        }

        throw new CRMValidationException(
            CRMErrorCodes.QuotationRequiresItems,
            $"Opportunity '{opportunity.OpportunityNumber}' carries no priced lines: "
            + "add items to the deal or send explicit quotation lines.");
    }

    /// <summary>
    /// Customer resolution (the CreateOpportunitySalesOrderCommandHandler precedent):
    /// direct customer link first, source lead's converted customer second.
    /// </summary>
    private async Task<Customer?> ResolveCustomerAsync(
        Opportunity opportunity,
        CancellationToken cancellationToken)
    {
        var direct = await _customers.GetByIdAsync(opportunity.PartyId, cancellationToken);
        if (direct is not null)
        {
            return direct;
        }

        var lead = await _crm.GetLeadByIdAsync(opportunity.PartyId, cancellationToken);
        if (lead?.ConvertedCustomerId is { } customerId && customerId != Guid.Empty)
        {
            return await _customers.GetByIdAsync(customerId, cancellationToken);
        }

        return null;
    }

    private async Task<IReadOnlyDictionary<Guid, string>> ResolveItemNamesAsync(
        IReadOnlyList<Guid> itemIds,
        CancellationToken cancellationToken)
    {
        var items = await _items.GetByIdsAsync(itemIds, cancellationToken);
        return items.ToDictionary(i => i.Id, i => i.ItemName);
    }
}
