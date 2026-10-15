using Erp.Domain.Entities;

namespace Erp.Application.DTOs;

/// <summary>Offline master bank catalog payload for GET /api/v1/banks (bank-catalog spec §4).</summary>
public sealed record BankDto(
    Guid Id,
    string CountryCode,
    string Code,
    string Name,
    string? SwiftBic,
    bool IsActive)
{
    public static BankDto From(Bank bank) =>
        new(
            bank.Id,
            bank.CountryCode,
            bank.Code,
            bank.Name,
            bank.SwiftBic,
            bank.IsActive);
}
