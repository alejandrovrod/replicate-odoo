using System;
using System.Collections.Generic;
using AssetHub.Domain.Assets;
using AssetHub.Domain.Finance;
using AssetHub.Domain.Incidents;
using AssetHub.Domain.Tasks;

namespace AssetHub.Domain.Maintenance;

public class MaintenanceOrder
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    
    public string Kind { get; set; } = MaintenanceOrderKinds.Corrective;
    public string State { get; set; } = MaintenanceOrderStates.Draft;
    
    public ICollection<WorkTask> WorkTasks { get; set; } = new List<WorkTask>();
    
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    
    public Guid AssetId { get; set; }
    public Asset? Asset { get; set; }
    
    public Guid? PreventivePlanId { get; set; }
    public PreventivePlan? PreventivePlan { get; set; }
    
    public Guid? IncidentId { get; set; }
    public Guid? WorkflowTemplateId { get; set; }
    public Incident? Incident { get; set; }
    
    public Guid? AssignedEmployeeId { get; set; }
    public AssetHub.Domain.Staff.Employee? AssignedEmployee { get; set; }
    

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ScheduledStart { get; set; }
    public DateTime? ScheduledEnd { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Physical dates (reliability analytics). Administrative dates above are fallbacks.
    public DateTime? FailureOccurredAt { get; set; }
    public DateTime? RepairStartedAt { get; set; }
    
    public decimal LaborCost { get; set; }
    
    public List<MaintenancePart> Parts { get; set; } = new();
    
public string PropertiesJson { get; set; } = "{}";

    // Finance navigation
    public AssetRepairCapitalization? RepairCapitalization { get; set; }

    public bool IsDeleted { get; set; }
}
