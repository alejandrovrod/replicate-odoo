using System;
using System.Collections.Generic;
using AssetHub.Domain.EntityTypes;

namespace AssetHub.Domain.AssetTemplates;

public class AssetTemplate
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    
    public Guid BusinessEntityTypeId { get; set; }
    public BusinessEntityType? BusinessEntityType { get; set; }

    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    public string SchemaJson { get; set; } = string.Empty;
    public List<Guid> AllowedChildTemplateIds { get; set; } = new();
    
    public LifecycleConfig LifecycleStates { get; set; } = new();
    
    public string MaintenanceChecklist { get; set; } = string.Empty;
    
    public int Version { get; set; } = 1;
    public bool IsActive { get; set; } = true;
}
