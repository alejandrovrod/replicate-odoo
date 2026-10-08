using Erp.Domain.Entities;

namespace Erp.Application.DTOs;

/// <summary>Currency payload returned by GET/POST/PUT /api/v1/currencies (RM-09).</summary>
public sealed record CurrencyDto(
    Guid Id,
    string Code,
    string Symbol,
    string FractionName,
    bool IsActive,
    DateTimeOffset CreatedAt,
    byte[]? RowVersion = null)
{
    public static CurrencyDto From(Currency currency) =>
        new(
            currency.Id,
            currency.Code,
            currency.Symbol,
            currency.FractionName,
            currency.IsActive,
            currency.CreatedAt,
            currency.RowVersion);
}
