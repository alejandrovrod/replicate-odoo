using System;

namespace AssetHub.Domain.Tenancy;

public class Subscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid PlanId { get; set; }
    public string Provider { get; set; } = "manual"; // stripe, manual
    public string? ExternalSubscriptionId { get; set; }
    public string Status { get; set; } = "trialing"; // trialing, active, past_due, canceled
    public DateTime? CurrentPeriodEnd { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
