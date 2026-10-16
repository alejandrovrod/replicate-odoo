using Erp.Domain.Entities;

namespace Erp.Application.DTOs;

/// <summary>One template line payload (mirrors <see cref="TaxTemplateItem"/>).</summary>
public sealed record TaxTemplateLineDto(
    Guid Id,
    Guid AccountId,
    string AccountCode,
    string AccountName,
    string Description,
    decimal Rate,
    decimal Amount,
    TaxChargeType ChargeType,
    TaxCalculationBasis CalculationBasis);

/// <summary>Tax template payload (tax-engine Fase 2 catalog).</summary>
public sealed record TaxTemplateDto(
    Guid Id,
    Guid CompanyId,
    string Title,
    string Category,
    bool IsDefault,
    IReadOnlyList<TaxTemplateLineDto> Lines)
{
    public static TaxTemplateDto From(TaxTemplate template) =>
        new(
            template.Id,
            template.CompanyId,
            template.Title,
            template.Category,
            template.IsDefault,
            template.Taxes
                .Select(l => new TaxTemplateLineDto(
                    l.Id,
                    l.AccountId,
                    l.Account?.AccountCode ?? string.Empty,
                    l.Account?.AccountName ?? string.Empty,
                    l.Description,
                    l.Rate,
                    l.Amount,
                    l.ChargeType,
                    l.CalculationBasis))
                .ToList());
}
