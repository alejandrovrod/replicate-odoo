using System;
using AssetHub.Domain.Assets;
using AssetHub.Domain.Incidents;
using AssetHub.Domain.Maintenance;

namespace AssetHub.Domain.Analytics;

public class CostEntry
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public Guid? AssetId { get; set; }
    public Asset? Asset { get; set; }

    public Guid? IncidentId { get; set; }
    public Incident? Incident { get; set; }

    public Guid? WorkOrderId { get; set; }
    public MaintenanceOrder? WorkOrder { get; set; }

    public string CostType { get; set; } = CostTypes.Other;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "MXN";
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public bool IsEstimated { get; set; }

    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }
}
