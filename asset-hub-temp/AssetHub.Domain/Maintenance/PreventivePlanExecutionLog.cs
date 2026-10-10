using System;

namespace AssetHub.Domain.Maintenance;

public class PreventivePlanExecutionLog
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public Guid PreventivePlanId { get; set; }
    public PreventivePlan? PreventivePlan { get; set; }

    public DateTime ExecutedAt { get; set; }
    public DateTime Occurrence { get; set; }

    public Guid AssetId { get; set; }

    public string Status { get; set; } = PreventivePlanConstants.ExecutionStatusSuccess;

    public string? GeneratedEntityType { get; set; }
    public Guid? GeneratedEntityId { get; set; }

    public string? Message { get; set; }
}
