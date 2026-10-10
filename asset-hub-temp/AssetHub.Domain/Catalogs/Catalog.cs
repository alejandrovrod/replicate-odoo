using System;

namespace AssetHub.Domain.Catalogs;

public class Catalog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? TargetModulesJson { get; set; }
    public bool IsSystem { get; set; }
}
