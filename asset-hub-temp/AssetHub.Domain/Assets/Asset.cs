using System;
using System.Collections.Generic;
using NetTopologySuite.Geometries;
using AssetHub.Domain.AssetTemplates;
using AssetHub.Domain.Finance;
using AssetHub.Domain.Incidents;
using AssetHub.Domain.Maintenance;
using AssetHub.Domain.Tasks;

namespace AssetHub.Domain.Assets;

public class Asset
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    
    public ICollection<AssetLifecycleEvent> LifecycleEvents { get; set; } = new List<AssetLifecycleEvent>();
    public ICollection<AssetConditionHistory> ConditionHistory { get; set; } = new List<AssetConditionHistory>();
    public ICollection<Incident> Incidents { get; set; } = new List<Incident>();
    public ICollection<MaintenanceOrder> MaintenanceOrders { get; set; } = new List<MaintenanceOrder>();
    public ICollection<WorkTask> WorkTasks { get; set; } = new List<WorkTask>();
    public ICollection<PreventivePlan> PreventivePlans { get; set; } = new List<PreventivePlan>();
    public ICollection<AssetHealthPrediction> HealthPredictions { get; set; } = new List<AssetHealthPrediction>();
    public ICollection<AssetMaterial> Materials { get; set; } = new List<AssetMaterial>();

    // Finance navigation properties
    public AssetFinanceBook? FinanceBook { get; set; }
    public ICollection<AssetDepreciationSchedule> DepreciationSchedules { get; set; } = new List<AssetDepreciationSchedule>();
    public ICollection<AssetDepreciationEntry> DepreciationEntries { get; set; } = new List<AssetDepreciationEntry>();
    public ICollection<AssetValueAdjustment> ValueAdjustments { get; set; } = new List<AssetValueAdjustment>();
    public ICollection<AssetDisposal> Disposals { get; set; } = new List<AssetDisposal>();
    public ICollection<AssetCustodyTransfer> CustodyTransfers { get; set; } = new List<AssetCustodyTransfer>();
    public ICollection<AssetRepairCapitalization> RepairCapitalizations { get; set; } = new List<AssetRepairCapitalization>();

    public Guid AssetTemplateId { get; set; }
    public AssetTemplate? AssetTemplate { get; set; }

    public Guid? ParentId { get; set; }
    public Asset? Parent { get; set; }

    public string Path { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PropertiesJson { get; set; } = string.Empty; // Store dynamic JSON here
    
    public Geometry? Geo { get; set; }
    public string? GeoType { get; set; } // Point, LineString, Polygon

    public DateTime? InstalledAt { get; set; }
    public DateTime? CommissionedAt { get; set; }
    
    public decimal? ConditionIndex { get; set; } // 0-100

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
