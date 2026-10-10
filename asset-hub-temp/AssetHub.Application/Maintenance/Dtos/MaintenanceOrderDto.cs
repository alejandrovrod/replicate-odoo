using System;

namespace AssetHub.Application.Maintenance.Dtos;

public class MaintenanceOrderSummaryDto
{
    public Guid Id { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ScheduledStart { get; set; }
    public DateTime? ScheduledEnd { get; set; }
    public DateTime? CompletedAt { get; set; }
    public decimal LaborCost { get; set; }
    public int PartsCount { get; set; }

    public Guid AssetId { get; set; }
    public string? AssetName { get; set; }

    public Guid? PreventivePlanId { get; set; }
    public string? PreventivePlanName { get; set; }

    public Guid? IncidentId { get; set; }
    public string? IncidentTitle { get; set; }
    
    public Guid? WorkflowTemplateId { get; set; }

    public Guid? AssignedEmployeeId { get; set; }
    public string? AssignedEmployeeName { get; set; }

    public string? PropertiesJson { get; set; }
}

public class MaintenanceOrderDetailDto : MaintenanceOrderSummaryDto
{
    public string? Description { get; set; }
    public MaintenanceOrderPartDto[] Parts { get; set; } = [];
    public MaintenanceOrderTaskSummaryDto[] Tasks { get; set; } = [];
}

public class MaintenanceOrderPartDto
{
    public Guid Id { get; set; }
    public Guid CatalogItemId { get; set; }
    public string? CatalogItemLabel { get; set; }
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost => Quantity * UnitCost;
}

public class MaintenanceOrderTaskSummaryDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public Guid? AssignedEmployeeId { get; set; }
    public string? AssignedEmployeeName { get; set; }
}
