using System;
using AssetHub.Domain.Catalogs;

namespace AssetHub.Domain.Assets;

public class AssetMaterial
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    
    public Guid AssetId { get; set; }
    public Asset? Asset { get; set; }
    
    public Guid CatalogItemId { get; set; }
    public CatalogItem? CatalogItem { get; set; }

    public decimal Quantity { get; set; }
    public string UnitOfMeasure { get; set; } = string.Empty;
    public bool IsCritical { get; set; }
    public string? Notes { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
