using System;

namespace AssetHub.Domain.Assets;

public class AssetAttachment
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid AssetId { get; set; }
    public Asset? Asset { get; set; }

    public string FileName { get; set; } = string.Empty;
    public string BlobUri { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Kind { get; set; } = string.Empty; // photo, doc, plan
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
