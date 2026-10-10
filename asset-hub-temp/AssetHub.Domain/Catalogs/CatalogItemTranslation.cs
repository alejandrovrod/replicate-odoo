using System;

namespace AssetHub.Domain.Catalogs;

public class CatalogItemTranslation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CatalogItemId { get; set; }
    public string Locale { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}
