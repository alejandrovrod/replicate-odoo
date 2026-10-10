using System;

namespace AssetHub.Domain.CommunicationTemplates;

public class NotificationMapping
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    
    // e.g. "Incident.Created", "Incident.Updated", "WorkOrder.Assigned"
    public string SystemEvent { get; set; } = string.Empty;
    
    // The template assigned to this event
    public Guid TemplateId { get; set; }
    public CommunicationTemplate? Template { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
