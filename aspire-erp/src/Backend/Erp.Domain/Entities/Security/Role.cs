using Erp.Domain.Common;

namespace Erp.Domain.Entities.Security;

public class Role : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsSystemDefault { get; set; }

    /// <summary>
    /// Derived root-role flag (plan.md Phase 1.2, no IsSystemRole column):
    /// <c>IsSystemDefault &amp;&amp; Name == "System Manager"</c>. Not mapped - it is
    /// computed from persisted columns, so it never appears in migrations.
    /// Prefer <see cref="RoleValidator.IsSystemManager(Role)"/> from guards.
    /// </summary>
    [global::System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public bool IsSystemManager =>
        IsSystemDefault && string.Equals(Name, RoleValidator.SystemManagerRoleName, StringComparison.Ordinal);

    public ICollection<DocTypePermission> Permissions { get; set; } = new List<DocTypePermission>();
}
