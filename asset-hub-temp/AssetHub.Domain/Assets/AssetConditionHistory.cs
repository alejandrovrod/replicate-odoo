using System;

namespace AssetHub.Domain.Assets;

public class AssetConditionHistory
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    
    public Guid AssetId { get; set; }
    public Asset Asset { get; set; } = null!;
    
    public decimal ConditionIndex { get; set; } // 0-100
    
    public DateTime CapturedAt { get; set; }
    public string? Reason { get; set; } // Opcional: "Maintenance", "Inspection", etc.
}
