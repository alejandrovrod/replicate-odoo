using Erp.Domain.Entities;

namespace Erp.Application.Features.Crm.DTOs;

/// <summary>Opportunity read model (Block B advance/board reads).</summary>
public sealed record OpportunityDto(
    Guid Id,
    Guid CompanyId,
    string OpportunityNumber,
    string Stage,
    string Status,
    decimal OpportunityAmount,
    decimal Probability,
    decimal WeightedAmount,
    string Currency,
    string? LossReason,
    string RowVersion)
{
    public static OpportunityDto Build(Opportunity opportunity) =>
        new(
            opportunity.Id,
            opportunity.CompanyId,
            opportunity.OpportunityNumber,
            opportunity.Stage,
            opportunity.Status,
            opportunity.OpportunityAmount,
            opportunity.Probability,
            opportunity.WeightedAmount,
            opportunity.Currency,
            opportunity.LossReason,
            opportunity.RowVersion is null || opportunity.RowVersion.Length == 0
                ? string.Empty
                : Convert.ToBase64String(opportunity.RowVersion));
}
