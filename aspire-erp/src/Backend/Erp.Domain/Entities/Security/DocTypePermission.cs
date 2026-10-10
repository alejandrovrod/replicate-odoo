using Erp.Domain.Common;

namespace Erp.Domain.Entities.Security;

public class DocTypePermission : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;

    public string DocType { get; set; } = string.Empty;

    public bool CanRead { get; set; }
    public bool CanWrite { get; set; }
    public bool CanCreate { get; set; }
    public bool CanDelete { get; set; }
    public bool CanSubmit { get; set; }
    public bool CanCancel { get; set; }
}
