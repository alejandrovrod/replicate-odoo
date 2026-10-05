using Erp.Domain.Common;

namespace Erp.Domain.Entities;

/// <summary>
/// Earning vs deduction classification of a <see cref="SalaryComponent"/>. Persisted as the
/// enum NAME (NVARCHAR(20) per the plan DDL), matching the RootType precedent from plan.md §7.3.
/// </summary>
public enum SalaryComponentType
{
    Earning,
    Deduction,
}

/// <summary>
/// Modular pay element (Task 12.2, plan.md §1 DDL table 4, spec ubiquitous language "Salary
/// Component"): Basic, Bonus, Transport Allowance (Earnings) or Income Tax, Social
/// Security/Pension, Health Insurance (Deductions). Every component maps to a leaf posting
/// account in the Chart of Accounts (<see cref="DefaultGLAccountId"/>), enforced by the
/// create handler through the Constitution III.3 guard.
/// </summary>
public class SalaryComponent : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid CompanyId { get; set; }

    public string ComponentName { get; set; } = string.Empty;

    public SalaryComponentType ComponentType { get; set; } = SalaryComponentType.Earning;

    /// <summary>
    /// When true, the Block B payroll engine prorates this component by PaymentDays
    /// (leave/attendance input). Stored now; consumed by Task 12.3, not this one.
    /// </summary>
    public bool DependsOnPaymentDays { get; set; }

    public bool IsTaxApplicable { get; set; } = true;

    public Guid DefaultGLAccountId { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
}
