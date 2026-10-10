using System;

namespace AssetHub.Domain.Security;

public class RolePermission
{
    public Guid TenantId { get; set; }
    public Guid RoleId { get; set; }
    public Guid PermissionId { get; set; }
}
