using System;

namespace AssetHub.Domain.Tasks;

public class TaskStatusHistory
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    
    public Guid WorkTaskId { get; set; }
    public WorkTask? WorkTask { get; set; }
    
    public string FromState { get; set; } = string.Empty;
    public string ToState { get; set; } = string.Empty;
    
    public Guid? ChangedByUserId { get; set; } // Null if changed by system job
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
