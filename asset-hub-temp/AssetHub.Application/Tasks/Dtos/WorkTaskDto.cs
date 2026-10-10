using System;

namespace AssetHub.Application.Tasks.Dtos;

public class WorkTaskDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string State { get; set; } = string.Empty;

    public Guid TaskTypeCatalogItemId { get; set; }
    public string? TaskTypeLabel { get; set; }

    public Guid PriorityCatalogItemId { get; set; }
    public string? PriorityLabel { get; set; }

    public DateTime? DueAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public Guid? AssetId { get; set; }
    public string? AssetName { get; set; }

    public Guid? IncidentId { get; set; }
    public string? IncidentTitle { get; set; }

    public Guid? WorkflowTemplateId { get; set; }

    public Guid? MaintenanceOrderId { get; set; }
    public string? MaintenanceOrderTitle { get; set; }
    public string? MaintenanceOrderState { get; set; }

    public Guid? PreventivePlanId { get; set; }
    public string? PreventivePlanName { get; set; }

    public Guid? TaskRecurrenceId { get; set; }
    public string? TaskRecurrenceName { get; set; }

    public Guid? AssignedEmployeeId { get; set; }
    public string? AssignedEmployeeName { get; set; }

    public Guid? AssignedTeamId { get; set; }
    public string? AssignedTeamName { get; set; }

    public string? PropertiesJson { get; set; }

    public bool IsIndependent { get; set; }
}
