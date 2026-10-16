using Erp.Domain.Entities;

namespace Erp.Application.DTOs;

/// <summary>
/// Supplier payload returned by GET/POST/PUT /api/v1/suppliers (Task 4.1, extended with the
/// buying-suppliers governance parity: group, multi-company accounts and hold status).
/// </summary>
public sealed record SupplierDto(
    Guid Id,
    string Code,
    string Name,
    string TaxId,
    Guid? DefaultPayableAccountId,
    Guid? CurrencyId,
    int PaymentTermsDays,
    decimal OutstandingAmount,
    bool IsActive,
    DateTimeOffset CreatedAt,
    byte[]? RowVersion = null,
    string? CustomData = null,
    Guid? SupplierGroupId = null,
    string? SupplierGroupCode = null,
    string? SupplierGroupName = null,
    Guid? PaymentTermsTemplateId = null,
    Guid? DefaultPriceListId = null,
    bool IsFrozen = false,
    bool OnHold = false,
    SupplierHoldType HoldType = SupplierHoldType.None,
    DateOnly? ReleaseDate = null,
    IReadOnlyList<SupplierCompanyAccountDto>? Accounts = null)
{
    public static SupplierDto From(Supplier supplier) =>
        new(
            supplier.Id,
            supplier.Code,
            supplier.Name,
            supplier.TaxId,
            supplier.DefaultPayableAccountId,
            supplier.CurrencyId,
            supplier.PaymentTermsDays,
            supplier.OutstandingAmount,
            supplier.IsActive,
            supplier.CreatedAt,
            supplier.RowVersion,
            supplier.CustomData,
            supplier.SupplierGroupId,
            supplier.SupplierGroup?.Code,
            supplier.SupplierGroup?.Name,
            supplier.PaymentTermsTemplateId,
            supplier.DefaultPriceListId,
            supplier.IsFrozen,
            supplier.OnHold,
            supplier.HoldType,
            supplier.ReleaseDate,
            supplier.Accounts.Select(SupplierCompanyAccountDto.From).ToList());
}
