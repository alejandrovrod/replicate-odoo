using System;
using AssetHub.Domain.Catalogs;

namespace AssetHub.Domain.Inventory;

public class StockBalance
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public Guid WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    public Guid CatalogItemId { get; set; }
    public CatalogItem? CatalogItem { get; set; }

    public decimal QuantityOnHand { get; set; }
    public decimal AverageUnitCost { get; set; }
    
    public DateTime UpdatedAt { get; set; }
    
    public byte[] RowVersion { get; set; } = Array.Empty<byte>(); // For optimistic concurrency
}
