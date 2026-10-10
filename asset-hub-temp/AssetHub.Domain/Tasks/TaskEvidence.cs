using System;

namespace AssetHub.Domain.Tasks;

public class TaskEvidence
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    
    public Guid WorkTaskId { get; set; }
    public WorkTask? WorkTask { get; set; }
    
    public string Type { get; set; } = string.Empty; // photo, signature, note, geocheck
    
    public string? BlobUri { get; set; }
    public string? Note { get; set; }
    
    // For geocheck
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    
    public Guid CapturedByUserId { get; set; }
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
}
