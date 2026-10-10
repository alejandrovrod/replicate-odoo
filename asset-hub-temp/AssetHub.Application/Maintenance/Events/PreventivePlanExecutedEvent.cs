using System;
using System.Collections.Generic;
using MediatR;

namespace AssetHub.Application.Maintenance.Events;

public class PreventivePlanExecutedEvent : INotification
{
    public Guid PlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public Guid TenantId { get; set; }

    public Guid? AssignedEmployeeId { get; set; }
    public Guid? AssignedTeamId { get; set; }

    public List<GeneratedItemInfo> GeneratedItems { get; set; } = new();
}

public class GeneratedItemInfo
{
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public Guid AssetId { get; set; }
    public string? AssetName { get; set; }
}
