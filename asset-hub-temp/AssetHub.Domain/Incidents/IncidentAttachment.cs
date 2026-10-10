using System;

namespace AssetHub.Domain.Incidents;

public class IncidentAttachment
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    
    public Guid IncidentId { get; set; }
    public Incident? Incident { get; set; }
    
    public string FileUrl { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
