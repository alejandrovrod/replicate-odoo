using Erp.Domain.Common;

namespace Erp.Domain.Entities;

/// <summary>
/// Master compensation template (Task 12.2, spec ubiquitous language "Salary Structure"):
/// the composition of base earnings and percentage/formulaic deductions for a role.
/// Header of the <see cref="SalaryStructureLine"/> aggregate - delete the structure, delete
/// its lines (Cascade, mirroring BOM). Deviation note: the plan DDL (§1) carries no
/// Structure/StructureLine/Assignment tables (only SalaryComponent, PayrollEntry, SalarySlip);
/// these three tables are ADDED here because Task 12.2's own text ("SalaryStructure linking
/// components to GL accounts") and the spec glossary both require them, and the Block B
/// payroll engine (Task 12.3) has nothing to price without a structure master.
/// </summary>
public class SalaryStructure : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid CompanyId { get; set; }

    public string StructureName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<SalaryStructureLine> Lines { get; set; } = new List<SalaryStructureLine>();
}
