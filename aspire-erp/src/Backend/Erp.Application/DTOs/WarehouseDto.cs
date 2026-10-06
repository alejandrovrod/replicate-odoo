using Erp.Domain.Entities;

namespace Erp.Application.DTOs;

/// <summary>Created-warehouse payload returned by POST /api/v1/warehouses (201 Created).</summary>
public sealed record WarehouseDto(
    Guid Id,
    Guid CompanyId,
    string Code,
    string Name,
    Guid? ParentWarehouseId,
    Guid AccountId,
    bool IsGroup,
    bool IsActive,
    byte[] RowVersion)
{
    public static WarehouseDto From(Warehouse warehouse) =>
        new(
            warehouse.Id,
            warehouse.CompanyId,
            warehouse.WarehouseCode,
            warehouse.WarehouseName,
            warehouse.ParentWarehouseId,
            warehouse.AccountId ?? Guid.Empty,
            warehouse.IsGroup,
            warehouse.IsActive,
            warehouse.RowVersion);
}

/// <summary>One node of the hierarchical warehouse tree returned by GET /api/v1/warehouses.</summary>
public sealed record WarehouseTreeNodeDto(
    Guid Id,
    string Code,
    string Name,
    Guid? ParentWarehouseId,
    Guid AccountId,
    bool IsGroup,
    bool IsActive,
    byte[] RowVersion,
    IReadOnlyList<WarehouseTreeNodeDto> Children);

