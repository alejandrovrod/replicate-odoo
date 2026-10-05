using Erp.Domain.Common;

namespace Erp.Domain.Entities;

/// <summary>
/// Organization node of a company's department tree (Task 12.1, plan.md §1 DDL table 1):
/// TenantId, CompanyId, DepartmentName (required, max 100), optional self parent, IsActive.
/// Tree rules live in <see cref="DepartmentValidator"/> (self-parent and cycle guards mirror
/// the <see cref="WarehouseValidator"/> ancestor-walk shape).
/// </summary>
public class Department : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid CompanyId { get; set; }

    public string DepartmentName { get; set; } = string.Empty;

    public Guid? ParentDepartmentId { get; set; }

    public Department? Parent { get; set; }

    public ICollection<Department> Children { get; set; } = new List<Department>();

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
}
