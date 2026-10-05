using Erp.Domain.Common;

namespace Erp.Domain.Entities;

/// <summary>
/// How an <see cref="Employee"/> receives net pay. Persisted as the enum NAME
/// (NVARCHAR(20) per the plan DDL), matching the RootType precedent from plan.md §7.3.
/// </summary>
public enum SalaryMode
{
    Bank,
    Cash,
    Cheque,
}

/// <summary>
/// Employment lifecycle of an <see cref="Employee"/> (plan.md §1 DDL Status values).
/// Persisted as the enum NAME (NVARCHAR(30) per the plan DDL), like
/// <see cref="AssetStatus"/> - unlike WorkOrder, whose DDL carries no such column type.
/// </summary>
public enum EmploymentStatus
{
    Active,
    Inactive,
    Suspended,
    Left,
}

/// <summary>
/// Staff personnel record (Task 12.1, plan.md §1 DDL table 3, spec ubiquitous language
/// "Employee"): legal identity, department/designation links, joining/relieving dates, bank
/// account details and employment status.
/// </summary>
/// <remarks>
/// Both <see cref="Status"/> (enum) and <see cref="IsActive"/> (bool) are kept per the plan
/// DDL: Status is the employment lifecycle, IsActive the soft-delete flag. Eligibility
/// (spec HR-03) reads Status only - IsActive is NOT part of the eligibility rule.
/// System-versioned (temporal) per the plan DDL; the (TenantId, CompanyId, EmployeeNumber)
/// uniqueness is DB-enforced (UQ_Employee_Tenant_Company_Code). <see cref="RowVersion"/> is a
/// justified addition the plan DDL omits: spec AS-06's concurrency precedent and every other
/// aggregate in this repo carry the token, and Block B slip generation is read-modify-write.
/// </remarks>
public class Employee : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid CompanyId { get; set; }

    /// <summary>Gapless staff code, unique per (TenantId, CompanyId) - enforced by the database.</summary>
    public string EmployeeNumber { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string WorkEmail { get; set; } = string.Empty;

    public Guid? DepartmentId { get; set; }

    public Department? Department { get; set; }

    public Guid? DesignationId { get; set; }

    public Designation? Designation { get; set; }

    public DateOnly DateOfJoining { get; set; }

    public DateOnly? DateOfRelieving { get; set; }

    public SalaryMode SalaryMode { get; set; } = SalaryMode.Bank;

    public string? BankName { get; set; }

    public string? BankAccountNumber { get; set; }

    public EmploymentStatus Status { get; set; } = EmploymentStatus.Active;

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Optimistic concurrency token (SQL Server <c>rowversion</c>): Block B slip generation is
    /// read-modify-write, so EF puts the original value in the UPDATE ... WHERE clause and a
    /// concurrent transition throws instead of being silently lost. Store-generated.
    /// </summary>
    public byte[] RowVersion { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Spec invariant HR-03, implemented literally: only employees with Status == Active whose
    /// employment start date is on or before the payroll period end date and who have not left
    /// prior to the period start date are eligible. Both boundaries are inclusive.
    /// </summary>
    public bool IsEligibleForPeriod(DateOnly periodStart, DateOnly periodEnd) =>
        Status == EmploymentStatus.Active
        && DateOfJoining <= periodEnd
        && (DateOfRelieving is null || DateOfRelieving >= periodStart);
}
