namespace Erp.Application.DTOs;

/// <summary>Aggregate snapshot behind GET /api/v1/stock/summary (all scalars, no rows).</summary>
public sealed record StockSummaryDto(
    int TotalSkus,
    int ActiveSkus,
    decimal TotalValue,
    int WarehouseCount,
    int LeafWarehouseCount);
