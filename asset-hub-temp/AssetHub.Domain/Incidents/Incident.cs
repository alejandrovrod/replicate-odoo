using System;
using System.Collections.Generic;
using NetTopologySuite.Geometries;
using AssetHub.Domain.Assets;
using AssetHub.Domain.Maintenance;
using AssetHub.Domain.Tasks;

namespace AssetHub.Domain.Incidents;

public class Incident
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    
    public Guid AssetId { get; set; }
    public Asset? Asset { get; set; }
    public Guid TypeId { get; set; }
    public Guid? PriorityId { get; set; }
    
    public Guid? WorkflowTemplateId { get; set; }
    public WorkflowTemplates.WorkflowTemplate? WorkflowTemplate { get; set; }

    public string PropertiesJson { get; set; } = "{}";

    public string State { get; set; } = IncidentStates.Reported;
    
    public ICollection<MaintenanceOrder> MaintenanceOrders { get; set; } = new List<MaintenanceOrder>();
    public ICollection<WorkTask> WorkTasks { get; set; } = new List<WorkTask>();

    public Geometry? Geo { get; set; }
    public string? GeoType { get; set; }
    
    public DateTime ReportedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    
    public bool IsDeleted { get; set; }
}
