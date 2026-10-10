using System;

namespace AssetHub.Domain.Tenancy;

public class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public TenantStatus Status { get; set; } = TenantStatus.Provisioning;
    public TenantMode Mode { get; set; } = TenantMode.Shared;
    public string? ConnectionString { get; set; }
    public Guid PlanId { get; set; }
    public string Locale { get; set; } = "es";
    public string TimeZone { get; set; } = "UTC";
    public string? LogoUrl { get; set; }
    public string? SupportEmail { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Configuración base de auditoría requerida por el spec (R3, R5, R8 - si aplica)
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
