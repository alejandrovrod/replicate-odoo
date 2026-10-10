using System;

namespace AssetHub.Application.Maintenance.Dtos;

public class PreventivePlanDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public string TargetType { get; set; } = string.Empty;
    public Guid? AssetTemplateId { get; set; }
    public string? AssetTemplateName { get; set; }
    public Guid? AssetId { get; set; }
    public string? AssetName { get; set; }
    public Guid? WorkflowTemplateId { get; set; }

    public string GeneratedEntityType { get; set; } = string.Empty;
    public string CronExpression { get; set; } = string.Empty;
    public int DueDateOffsetDays { get; set; }
    public string? ConditionRuleJson { get; set; }

    public bool AutoAssign { get; set; }
    public Guid? DefaultAssignedEmployeeId { get; set; }
    public Guid? DefaultAssignedTeamId { get; set; }

    public DateTime? NextRunAt { get; set; }
    public DateTime? LastRunAt { get; set; }
    public DateTime? EndsAt { get; set; }

    public bool IsActive { get; set; }
}
