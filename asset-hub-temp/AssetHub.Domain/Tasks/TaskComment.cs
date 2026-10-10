using System;

namespace AssetHub.Domain.Tasks;

public class TaskComment
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    
    public Guid WorkTaskId { get; set; }
    public WorkTask? WorkTask { get; set; }
    
    public string Text { get; set; } = string.Empty;
    
    public Guid AuthorUserId { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
