using Erp.Domain.Entities;

namespace Erp.Application.Features.Crm.DTOs;

/// <summary>One priced deal line (mirrors <see cref="OpportunityItem"/>).</summary>
public sealed record OpportunityItemDto(
    Guid Id,
    Guid ItemId,
    string ItemName,
    decimal Quantity,
    decimal Rate,
    decimal DiscountPercentage,
    decimal Amount)
{
    public static OpportunityItemDto Build(OpportunityItem item) =>
        new(
            item.Id,
            item.ItemId,
            item.ItemName,
            item.Quantity,
            item.Rate,
            item.DiscountPercentage,
            item.Amount);
}

/// <summary>One follow-up log row of the deal (mirrors <see cref="CRMActivity"/>).</summary>
public sealed record OpportunityActivityDto(
    Guid Id,
    string Type,
    string Subject,
    string? Content,
    DateTimeOffset ActivityDate,
    DateOnly? NextFollowUpDate,
    Guid CreatedByUserId)
{
    public static OpportunityActivityDto Build(CRMActivity activity) =>
        new(
            activity.Id,
            activity.Type.ToString(),
            activity.Subject,
            activity.Content,
            activity.ActivityDate,
            activity.NextFollowUpDate,
            activity.CreatedByUserId);
}

/// <summary>One quotation issued from the deal (detail-dialog history row).</summary>
public sealed record OpportunityQuotationDto(
    Guid Id,
    string QuotationNo,
    string Status,
    decimal TotalAmount,
    DateOnly TransactionDate)
{
    public static OpportunityQuotationDto Build(Quotation quotation) =>
        new(
            quotation.Id,
            quotation.QuotationNo,
            quotation.Status.ToString(),
            quotation.TotalAmount,
            quotation.TransactionDate);
}

/// <summary>
/// Full commercial dossier of a deal (detail drawer source): header money with base
/// conversion, priced lines, segmentation, competitors, loss data, activities and the
/// quotations issued from it.
/// </summary>
public sealed record OpportunityDetailDto(
    Guid Id,
    Guid CompanyId,
    string OpportunityNumber,
    string OpportunityFrom,
    Guid PartyId,
    string PartyName,
    string Stage,
    string Status,
    decimal OpportunityAmount,
    decimal Probability,
    decimal WeightedAmount,
    Guid? CurrencyId,
    string CurrencyCode,
    decimal ExchangeRate,
    decimal BaseOpportunityAmount,
    DateOnly ExpectedClosingDate,
    Guid? TerritoryId,
    string? TerritoryName,
    Guid? IndustryId,
    string? IndustryName,
    string? MarketSegment,
    IReadOnlyList<string> Competitors,
    Guid? LossReasonId,
    string? LossReasonName,
    string? LossReason,
    Guid? AssignedSalespersonId,
    string RowVersion,
    IReadOnlyList<OpportunityItemDto> Items,
    IReadOnlyList<OpportunityActivityDto> Activities,
    IReadOnlyList<OpportunityQuotationDto> Quotations);
