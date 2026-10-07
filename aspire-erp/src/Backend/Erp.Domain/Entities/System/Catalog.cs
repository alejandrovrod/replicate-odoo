using Erp.Domain.Common;

namespace Erp.Domain.Entities.System;

public class Catalog : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid TenantId { get; set; }
    
    /// <summary>
    /// If null, this is a global catalog. If set, it's specific to a tenant.
    /// </summary>
    public Guid? CompanyId { get; set; }
    
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// System catalogs cannot be deleted, but items might be added depending on business rules.
    /// </summary>
    public bool IsSystem { get; set; }
    
    public ICollection<CatalogItem> Items { get; set; } = new List<CatalogItem>();
    
    public DateTimeOffset CreatedAt { get; set; }
}
