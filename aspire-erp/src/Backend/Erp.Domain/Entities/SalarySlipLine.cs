namespace Erp.Domain.Entities;

/// <summary>
/// One priced component row of a <see cref="SalarySlip"/> (Task 12.3): the HR-01 itemized stub.
/// Justified addition the plan DDL lacks (same note as the structure tables): without stored
/// lines the slip cannot show "itemized statutory deductions", and the accrual voucher cannot
/// be built per component GL account.
/// </summary>
/// <remarks>
/// <see cref="ComponentName"/> and <see cref="ComponentType"/> are snapshots, not live joins:
/// the slip is a forensic record (renaming a component later must not rewrite history).
/// Aggregate child of <see cref="SalarySlip"/> (no TenantId of its own, the BomItem precedent).
/// </remarks>
public class SalarySlipLine
{
    public Guid Id { get; set; }

    public Guid SlipId { get; set; }

    public SalarySlip? Slip { get; set; }

    public Guid ComponentId { get; set; }

    public SalaryComponent? Component { get; set; }

    /// <summary>Component display name at pricing time (NVARCHAR(100) snapshot).</summary>
    public string ComponentName { get; set; } = string.Empty;

    /// <summary>Earning vs deduction side at pricing time (persisted as the enum NAME).</summary>
    public SalaryComponentType ComponentType { get; set; } = SalaryComponentType.Earning;

    /// <summary>Final priced amount of this line (decimal(18,4), post-proration, &gt;= 0).</summary>
    public decimal Amount { get; set; }
}
