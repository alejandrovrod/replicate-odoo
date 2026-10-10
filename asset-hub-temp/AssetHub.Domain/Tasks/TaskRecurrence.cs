using System;

namespace AssetHub.Domain.Tasks;

public class TaskRecurrence
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    
    public string? CronExpression { get; set; }
    public int? IntervalDays { get; set; }
    
    public DateTime NextRunAt { get; set; }
    public DateTime? EndsAt { get; set; }
    
    public string TaskTemplateJson { get; set; } = string.Empty;
    
    public bool IsActive { get; set; } = true;
}
