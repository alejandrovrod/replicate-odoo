using System;
using System.Collections.Generic;
using AssetHub.Domain.AssetTemplates;
using AssetHub.Domain.Assets;
using AssetHub.Domain.Tasks;

namespace AssetHub.Domain.Maintenance;

public class PreventivePlan
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public Guid? AssetTemplateId { get; set; }
    public AssetTemplate? AssetTemplate { get; set; }

    public Guid? AssetId { get; set; }
    public Asset? Asset { get; set; }

    public ICollection<WorkTask> WorkTasks { get; set; } = new List<WorkTask>();
    public ICollection<MaintenanceOrder> MaintenanceOrders { get; set; } = new List<MaintenanceOrder>();

    public Guid? WorkflowTemplateId { get; set; }
    public AssetHub.Domain.WorkflowTemplates.WorkflowTemplate? WorkflowTemplate { get; set; }

    public string GeneratedEntityType { get; set; } = PreventivePlanConstants.GeneratedEntityTypeWorkTask;
    public string CronExpression { get; set; } = string.Empty;

    public int DueDateOffsetDays { get; set; } = 7;
    public string? ConditionRuleJson { get; set; }

    public Guid? DefaultAssignedEmployeeId { get; set; }
    public Guid? DefaultAssignedTeamId { get; set; }
    public bool AutoAssign { get; set; }

    public DateTime? NextRunAt { get; set; }
    public DateTime? LastRunAt { get; set; }
    public DateTime? EndsAt { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
}
