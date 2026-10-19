using Erp.Application.Common;
using Erp.Application.Features.Crm.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Crm.Queries;

/// <summary>Full commercial dossier of one deal (detail drawer source), or null.</summary>
public sealed record GetOpportunityDetailQuery(
    Guid CompanyId,
    Guid OpportunityId) : IQuery<OpportunityDetailDto?>;

public sealed class GetOpportunityDetailQueryHandler
    : IQueryHandler<GetOpportunityDetailQuery, OpportunityDetailDto?>
{
    private readonly ICrmRepository _crm;
    private readonly ICrmCatalogRepository _catalogs;
    private readonly ICurrencyRepository _currencies;

    public GetOpportunityDetailQueryHandler(
        ICrmRepository crm,
        ICrmCatalogRepository catalogs,
        ICurrencyRepository currencies)
    {
        _crm = crm;
        _catalogs = catalogs;
        _currencies = currencies;
    }

    public async Task<OpportunityDetailDto?> HandleAsync(
        GetOpportunityDetailQuery query,
        CancellationToken cancellationToken = default)
    {
        var opportunity = await _crm.GetOpportunityByIdAsync(query.OpportunityId, cancellationToken);
        if (opportunity is null || opportunity.CompanyId != query.CompanyId)
        {
            return null;
        }

        // Small bounded catalogs: two list reads beat N+1 name lookups and stay tenant-safe
        // (the repository already scopes to the ambient tenant).
        var lossReasons = await _catalogs.ListLossReasonsAsync(cancellationToken);
        var territories = await _catalogs.ListTerritoriesAsync(cancellationToken);
        var industries = await _catalogs.ListIndustriesAsync(cancellationToken);

        var currencyCode = "USD";
        if (opportunity.CurrencyId.HasValue)
        {
            currencyCode = (await _currencies.GetByIdAsync(
                opportunity.CurrencyId.Value, cancellationToken))?.Code ?? "USD";
        }

        return new OpportunityDetailDto(
            opportunity.Id,
            opportunity.CompanyId,
            opportunity.OpportunityNumber,
            opportunity.OpportunityFrom,
            opportunity.PartyId,
            opportunity.PartyName,
            opportunity.Stage,
            opportunity.Status,
            opportunity.OpportunityAmount,
            opportunity.Probability,
            opportunity.WeightedAmount,
            opportunity.CurrencyId,
            currencyCode,
            opportunity.ExchangeRate,
            opportunity.BaseOpportunityAmount,
            opportunity.ExpectedClosingDate,
            opportunity.TerritoryId,
            territories.FirstOrDefault(t => t.Id == opportunity.TerritoryId)?.Name,
            opportunity.IndustryId,
            industries.FirstOrDefault(t => t.Id == opportunity.IndustryId)?.Name,
            opportunity.MarketSegment,
            opportunity.Competitors.ToList(),
            opportunity.LossReasonId,
            lossReasons.FirstOrDefault(r => r.Id == opportunity.LossReasonId)?.Name,
            opportunity.LossReason,
            opportunity.AssignedSalespersonId,
            opportunity.RowVersion is null || opportunity.RowVersion.Length == 0
                ? string.Empty
                : Convert.ToBase64String(opportunity.RowVersion),
            opportunity.Items.Select(OpportunityItemDto.Build).ToList(),
            opportunity.Activities
                .OrderBy(a => a.ActivityDate)
                .ThenBy(a => a.Id)
                .Select(OpportunityActivityDto.Build)
                .ToList(),
            opportunity.Quotations.Select(OpportunityQuotationDto.Build).ToList());
    }
}
