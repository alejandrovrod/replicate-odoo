using Erp.Domain.Entities;

namespace Erp.Application.DTOs;

/// <summary>Supplier payload returned by GET/POST /api/v1/suppliers (Task 4.1).</summary>
public sealed record SupplierDto(
    Guid Id,
    string Code,
    string Name,
    bool IsActive,
    DateTimeOffset CreatedAt)
{
    public static SupplierDto From(Supplier supplier) =>
        new(
            supplier.Id,
            supplier.Code,
            supplier.Name,
            supplier.IsActive,
            supplier.CreatedAt);
}
