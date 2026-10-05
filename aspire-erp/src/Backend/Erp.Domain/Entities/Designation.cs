using Erp.Domain.Common;

namespace Erp.Domain.Entities;

/// <summary>
/// Job role master of a company (Task 12.1, plan.md §1 DDL table 2): TenantId, CompanyId,
/// DesignationName (required, max 100), IsActive. Referenced by <see cref="Employee"/>.
/// </summary>
public class Designation : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid CompanyId { get; set; }

    public string DesignationName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
}
