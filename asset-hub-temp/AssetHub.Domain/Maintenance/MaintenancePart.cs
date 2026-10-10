using System;
using AssetHub.Domain.Catalogs;

namespace AssetHub.Domain.Maintenance;

public class MaintenancePart
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    
    public Guid MaintenanceOrderId { get; set; }
    public MaintenanceOrder? MaintenanceOrder { get; set; }
    
    public Guid CatalogItemId { get; set; }
    public CatalogItem? CatalogItem { get; set; }
    
    public int Quantity { get; set; } // Kept as int for historical compatibility, or we could change to decimal if safe
    public decimal UnitCost { get; set; }
    
    // Inventory V1 fields
    public Guid? WarehouseId { get; set; }
    public Guid? InventoryTransactionId { get; set; }
    public string SourceType { get; set; } = "external"; // internal or external
    public string? ExternalSupplierName { get; set; }
    public string? ExternalReference { get; set; }
}
