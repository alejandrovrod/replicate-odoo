using System;
using AssetHub.Domain.Assets;
using AssetHub.Domain.Catalogs;
using AssetHub.Domain.Incidents;
using AssetHub.Domain.Maintenance;
using AssetHub.Domain.Staff;

namespace AssetHub.Domain.Tasks;

public class WorkTask
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    
    public string State { get; set; } = WorkTaskStates.Todo;
    
    public Guid TaskTypeCatalogItemId { get; set; }
    public CatalogItem? TaskTypeCatalogItem { get; set; }
    
    public Guid PriorityCatalogItemId { get; set; }
    public CatalogItem? PriorityCatalogItem { get; set; }
    
    public DateTime? DueAt { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? DueDate { get; set; }

    public ICollection<TaskStatusHistory> StatusHistory { get; set; } = new List<TaskStatusHistory>();
    public ICollection<TaskComment> TaskComments { get; set; } = new List<TaskComment>();
    
    public bool IsIndependent { get; set; }
    
    // Links (optional, but at least one must be present if not independent)
    public Guid? AssetId { get; set; }
    public Asset? Asset { get; set; }
    
    public Guid? MaintenanceOrderId { get; set; }
    public MaintenanceOrder? MaintenanceOrder { get; set; }
    
    public Guid? IncidentId { get; set; }
    public Incident? Incident { get; set; }
    
    // Assignment
    public Guid? AssignedEmployeeId { get; set; }
    public Employee? AssignedEmployee { get; set; }
    
    public Guid? AssignedTeamId { get; set; }
    public Team? AssignedTeam { get; set; }
    
    // Generator
    public Guid? TaskRecurrenceId { get; set; }
    public TaskRecurrence? TaskRecurrence { get; set; }

    public Guid? PreventivePlanId { get; set; }
    public PreventivePlan? PreventivePlan { get; set; }
    
    public string PropertiesJson { get; set; } = "{}";

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
