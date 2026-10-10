using System;

namespace AssetHub.Domain.Incidents;

public class IncidentLifecycleEvent
{
    public Guid Id { get; set; }
    public Guid IncidentId { get; set; }
    public Incident? Incident { get; set; }

    public string EventType { get; set; } = string.Empty; // reportado, triaged, cambio estado, cerrado, cancelado
    public string FromState { get; set; } = string.Empty;
    public string ToState { get; set; } = string.Empty;
    
    public string? Notes { get; set; }
    public string? PropertiesJson { get; set; } // Opcional: Payload de la transición
    
    public DateTime At { get; set; }
    public Guid UserId { get; set; }
}
