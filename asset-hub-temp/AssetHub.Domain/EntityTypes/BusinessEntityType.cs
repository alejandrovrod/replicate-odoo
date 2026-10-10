using System;
using System.Collections.Generic;

namespace AssetHub.Domain.EntityTypes;

public class BusinessEntityType
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public List<string> EnabledModules { get; set; } = new();
    public List<Guid> DefaultCatalogIds { get; set; } = new();
    public bool IsActive { get; set; } = true;
}
