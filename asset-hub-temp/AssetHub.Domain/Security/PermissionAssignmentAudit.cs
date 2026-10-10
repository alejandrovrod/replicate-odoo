using System;

namespace AssetHub.Domain.Security;

public class PermissionAssignmentAudit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid AdminUserId { get; set; }
    public Guid RoleId { get; set; }
    public Guid PermissionId { get; set; }
    public bool Granted { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
    public string? Ip { get; set; }
}
