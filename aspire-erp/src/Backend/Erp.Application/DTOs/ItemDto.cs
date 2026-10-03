using Erp.Domain.Entities;

namespace Erp.Application.DTOs;

/// <summary>Stock level of one item inside one warehouse (current SUM of the Kardex rows).</summary>
public sealed record ItemStockDto(
    Guid WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    decimal Qty,
    decimal Value);

/// <summary>
/// Item payload returned by GET/POST /api/v1/items. <see cref="Stock"/> is scoped to the company
/// passed to the query (items themselves are tenant-wide - see <see cref="Item"/>).
/// </summary>
public sealed record ItemDto(
    Guid Id,
    string Code,
    string Name,
    ValuationMethod ValuationMethod,
    Guid BaseUOMId,
    Guid? IncomeAccountId,
    Guid? ExpenseAccountId,
    bool IsActive,
    IReadOnlyList<ItemStockDto> Stock)
{
    public static ItemDto From(Item item, IReadOnlyList<ItemStockDto>? stock = null) =>
        new(
            item.Id,
            item.ItemCode,
            item.ItemName,
            item.ValuationMethod,
            item.StockUomId,
            null,
            null,
            item.IsActive,
            stock ?? (IReadOnlyList<ItemStockDto>)Array.Empty<ItemStockDto>());
}

