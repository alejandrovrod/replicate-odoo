using Erp.Domain.Entities;

namespace Erp.Application.DTOs;

/// <summary>Supplier payload returned by GET/POST /api/v1/suppliers (Task 4.1).</summary>
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
    byte[]? RowVersion = null)
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
            supplier.RowVersion);
}
