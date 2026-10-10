using System;

namespace AssetHub.Domain.Assets;

public class AssetLifecycleEvent
{
    public Guid Id { get; set; }
    public Guid AssetId { get; set; }
    public Asset? Asset { get; set; }

    public string EventType { get; set; } = string.Empty; // alta, cambio estado, intervencion, baja
    public string FromState { get; set; } = string.Empty;
    public string ToState { get; set; } = string.Empty;
    
    public string? Notes { get; set; }
    public DateTime At { get; set; }
    public Guid UserId { get; set; }
}
