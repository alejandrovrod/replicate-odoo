using Erp.Domain.Common;

namespace Erp.Domain.Entities.System;

public class CatalogItemTranslation : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid TenantId { get; set; }
    
    public Guid CatalogItemId { get; set; }
    
    /// <summary>
    /// ISO Language code, e.g. "en", "es", "fr"
    /// </summary>
    public string LanguageCode { get; set; } = string.Empty;
    
    public string TranslatedName { get; set; } = string.Empty;
    
    public CatalogItem? CatalogItem { get; set; }
    
    public DateTimeOffset CreatedAt { get; set; }
}
