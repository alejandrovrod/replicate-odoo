using System;

namespace AssetHub.Application.Tasks.Dtos;

public class WorkTaskSummaryDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public DateTime? DueAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public Guid? AssetId { get; set; }
    public string? AssetName { get; set; }

    public Guid? IncidentId { get; set; }
    public string? IncidentTitle { get; set; }

    public Guid? WorkflowTemplateId { get; set; }

    public Guid? PreventivePlanId { get; set; }
    public string? PreventivePlanName { get; set; }

    public Guid? MaintenanceOrderId { get; set; }
    public string? MaintenanceOrderTitle { get; set; }
    public string? MaintenanceOrderState { get; set; }

    public Guid? AssignedEmployeeId { get; set; }
    public string? AssignedEmployeeName { get; set; }

    public Guid? AssignedTeamId { get; set; }
    public string? AssignedTeamName { get; set; }

    public string? PriorityLabel { get; set; }
}
