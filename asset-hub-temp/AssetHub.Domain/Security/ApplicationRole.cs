using System;
using Microsoft.AspNetCore.Identity;

namespace AssetHub.Domain.Security;

public class ApplicationRole : IdentityRole<Guid>
{
    public Guid? TenantId { get; set; }

    // true = rol base del sistema (seed); false = rol custom del tenant
    public bool IsSystemDefault { get; set; }

    public string Description { get; set; } = string.Empty;
}
