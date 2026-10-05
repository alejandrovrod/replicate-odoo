using Erp.Domain.Common;

namespace Erp.Domain.Entities;

/// <summary>
/// Binding contract linking an <see cref="Employee"/> to a <see cref="SalaryStructure"/> from
/// an effective start date (Task 12.2, spec ubiquitous language "Salary Structure
/// Assignment"). Open-ended while <see cref="EffectiveTo"/> is null; assignments of one
/// employee must never overlap (see
/// <see cref="SalaryStructureValidator.EnsureNoOverlap"/>).
/// </summary>
public class SalaryStructureAssignment : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid CompanyId { get; set; }

    public Guid EmployeeId { get; set; }

    public Employee? Employee { get; set; }

    public Guid StructureId { get; set; }

    public SalaryStructure? Structure { get; set; }

    public DateOnly EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }

    public bool IsActive { get; set; } = true;
}
