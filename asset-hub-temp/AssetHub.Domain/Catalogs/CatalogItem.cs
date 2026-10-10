using System;
using System.Collections.Generic;

namespace AssetHub.Domain.Catalogs;

public class CatalogItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CatalogId { get; set; }
    public Guid? TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public Guid? ParentItemId { get; set; }
    public int Order { get; set; }
    public string? MetadataJson { get; set; }
    public bool IsDeleted { get; set; }
    
    // Navigation properties for EF
    public Catalog? Catalog { get; set; }
    public ICollection<CatalogItemTranslation> Translations { get; set; } = new List<CatalogItemTranslation>();
}
