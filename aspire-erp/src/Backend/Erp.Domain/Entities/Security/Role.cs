using Erp.Domain.Common;

namespace Erp.Domain.Entities.Security;

public class Role : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsSystemDefault { get; set; }

    public ICollection<DocTypePermission> Permissions { get; set; } = new List<DocTypePermission>();
}
