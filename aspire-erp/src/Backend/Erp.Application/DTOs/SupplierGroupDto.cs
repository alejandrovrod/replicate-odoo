using Erp.Domain.Entities;

namespace Erp.Application.DTOs;

/// <summary>Supplier group catalog entry (module buying-suppliers, spec §1 gap D1).</summary>
public sealed record SupplierGroupDto(
    Guid Id,
    string Code,
    string Name,
    bool IsActive,
    DateTimeOffset CreatedAt)
{
    public static SupplierGroupDto From(SupplierGroup group) =>
        new(group.Id, group.Code, group.Name, group.IsActive, group.CreatedAt);
}
