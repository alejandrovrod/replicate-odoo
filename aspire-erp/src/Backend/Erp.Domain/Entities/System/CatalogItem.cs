using Erp.Domain.Common;

namespace Erp.Domain.Entities.System;

public class CatalogItem : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid TenantId { get; set; }
    
    public Guid CatalogId { get; set; }
    
    /// <summary>
    /// If null, this item is global. If set, it belongs to a specific tenant.
    /// </summary>
    public Guid? CompanyId { get; set; }
    
    public string Code { get; set; } = string.Empty;
    public string DefaultName { get; set; } = string.Empty;
    
    public int SortOrder { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    public Catalog? Catalog { get; set; }
    public ICollection<CatalogItemTranslation> Translations { get; set; } = new List<CatalogItemTranslation>();
    
    public DateTimeOffset CreatedAt { get; set; }
}
