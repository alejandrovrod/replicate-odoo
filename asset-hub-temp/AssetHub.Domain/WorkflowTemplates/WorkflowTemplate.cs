using System;
using System.Collections.Generic;

namespace AssetHub.Domain.WorkflowTemplates;

public class WorkflowTemplate
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    public string SchemaJson { get; set; } = string.Empty;
    
    public string Type { get; set; } = "incident";
    public AssetTemplates.LifecycleConfig LifecycleStates { get; set; } = new();
    
    public int Version { get; set; } = 1;
    public bool IsActive { get; set; } = true;
}
