using System;
using Microsoft.AspNetCore.Identity;

namespace AssetHub.Domain.Security;

public class ApplicationUser : IdentityUser<Guid>
{
    public Guid? TenantId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string AvatarUrl { get; set; } = string.Empty;
    public string PreferredLocale { get; set; } = "es";
}
