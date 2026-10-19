using Erp.Domain.Entities;

namespace Erp.Application.Features.Crm.DTOs;

/// <summary>One priced quotation line (mirrors <see cref="QuotationItem"/>).</summary>
public sealed record QuotationItemDto(
    Guid Id,
    Guid ItemId,
    string ItemName,
    decimal Quantity,
    decimal Rate,
    decimal Amount);

/// <summary>Formal quotation payload (detail read + create-quotation 201 body).</summary>
public sealed record QuotationDto(
    Guid Id,
    Guid CompanyId,
    string QuotationNo,
    Guid? OpportunityId,
    Guid CustomerId,
    DateOnly TransactionDate,
    DateOnly ValidTill,
    string Status,
    decimal TotalAmount,
    string Currency,
    decimal ExchangeRate,
    DateTimeOffset CreatedAt,
    IReadOnlyList<QuotationItemDto> Items)
{
    public static QuotationDto Build(Quotation quotation, IReadOnlyDictionary<Guid, string>? itemNames = null)
    {
        static string NameOf(Guid itemId, IReadOnlyDictionary<Guid, string>? names) =>
            names is not null && names.TryGetValue(itemId, out var name) ? name : string.Empty;

        return new QuotationDto(
            quotation.Id,
            quotation.CompanyId,
            quotation.QuotationNo,
            quotation.OpportunityId,
            quotation.CustomerId,
            quotation.TransactionDate,
            quotation.ValidTill,
            quotation.Status.ToString(),
            quotation.TotalAmount,
            quotation.Currency,
            quotation.ExchangeRate,
            quotation.CreatedAt,
            quotation.Items
                .Select(i => new QuotationItemDto(
                    i.Id, i.ItemId, NameOf(i.ItemId, itemNames), i.Quantity, i.Rate, i.Amount))
                .ToList());
    }
}

/// <summary>One row of the read-only segmentation catalogs (loss reasons, territories, industries).</summary>
public sealed record CrmCatalogDto(Guid Id, string Code, string Name);
