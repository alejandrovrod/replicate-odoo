using Erp.Domain.Entities;

namespace Erp.Domain.Repositories;

/// <summary>
/// Data-access contract for the HR &amp; Payroll masters (Tasks 12.1-12.2): departments,
/// designations, employees, salary components, structures, lines and assignments.
/// Implemented by Erp.Infrastructure.Data.Repositories.HrPayrollRepository. Minimal by design -
/// Block B (Tasks 12.3-12.4) grows it with payroll-entry/slip reads (same precedent as the
/// manufacturing repository, which grew from masters to workflow).
/// </summary>
/// <remarks>
/// All writes happen through <see cref="ExecuteInTransactionAsync{T}"/> so multi-row creates
/// (structure header + lines) commit atomically. Tenant isolation stays automatic
/// (Constitution II.3); CompanyId predicates are business scoping, not tenancy.
/// </remarks>
public interface IHrPayrollRepository
{
    /// <summary>
    /// Runs <paramref name="operation"/> inside ONE database transaction and commits only when
    /// it returns; any exception rolls the whole write back. Joins an already-open transaction
    /// so nested calls compose instead of dead-locking (same contract as IStockRepository -
    /// both share the same scoped AppDbContext).
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);

    // Departments
    Task AddDepartmentAsync(Department department, CancellationToken cancellationToken = default);
    Task<Department?> GetDepartmentByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the requested department followed by its ancestors - parent, grandparent, ...,
    /// root (index 0 = the department itself). Empty list when it does not exist (or belongs
    /// to another tenant, which the global query filter treats as non-existent). Mirrors
    /// IWarehouseRepository.GetByIdWithAncestorsAsync (cycle prevention for the tree).
    /// </summary>
    Task<IReadOnlyList<Department>> GetDepartmentWithAncestorsAsync(Guid departmentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Department>> GetDepartmentsByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default);

    // Designations
    Task AddDesignationAsync(Designation designation, CancellationToken cancellationToken = default);
    Task<Designation?> GetDesignationByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Designation>> GetDesignationsByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default);

    // Employees
    Task AddEmployeeAsync(Employee employee, CancellationToken cancellationToken = default);
    Task UpdateEmployeeAsync(Employee employee, CancellationToken cancellationToken = default);
    Task<Employee?> GetEmployeeByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Employee?> GetEmployeeByNumberAsync(Guid companyId, string employeeNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Employee>> GetEmployeesByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default);

    // Salary components
    Task AddComponentAsync(SalaryComponent component, CancellationToken cancellationToken = default);
    Task<SalaryComponent?> GetComponentByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SalaryComponent>> GetComponentsByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default);

    // Salary structures + lines
    Task AddStructureAsync(SalaryStructure structure, CancellationToken cancellationToken = default);
    Task<SalaryStructure?> GetStructureByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SalaryStructure>> GetStructuresByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default);

    // Assignments
    Task AddAssignmentAsync(SalaryStructureAssignment assignment, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SalaryStructureAssignment>> GetAssignmentsByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);

    /// <summary>All assignments of one company - the batch-eligibility read (Task 12.3).</summary>
    Task<IReadOnlyList<SalaryStructureAssignment>> GetAssignmentsByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default);

    // Payroll batch engine (Tasks 12.3-12.4): entries, slips, slip lines and the GL writes.
    // Same growth precedent as the manufacturing repository (masters first, workflow second).

    /// <summary>
    /// Gapless batch number (Constitution III.4): SELECT MAX(PayrollNumber) WITH
    /// (UPDLOCK, HOLDLOCK) inside the AMBIENT submit transaction, scoped to
    /// (TenantId, CompanyId, year). A rolled-back submit consumes NO number.
    /// </summary>
    Task<string> NextPayrollNumberAsync(Guid companyId, int year, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gapless GL voucher number (Constitution III.4): SELECT MAX(VoucherNo) WITH
    /// (UPDLOCK, HOLDLOCK) over dbo.GLEntry inside the AMBIENT posting transaction, scoped to
    /// (TenantId, CompanyId, prefix-year). The payroll GL prefix is PYR.
    /// </summary>
    Task<string> NextVoucherNumberAsync(Guid companyId, string prefix, int year, CancellationToken cancellationToken = default);

    /// <summary>
    /// HR-06 live race guard: takes the UPDLOCK/HOLDLOCK range lock over one entry's slip rows
    /// (held to transaction end) so concurrent slip inserts for the same entry serialize instead
    /// of racing past the existence check. The DB unique index stays the authority.
    /// </summary>
    Task LockEntrySlipsAsync(Guid payrollEntryId, CancellationToken cancellationToken = default);

    Task AddPayrollEntryAsync(PayrollEntry entry, CancellationToken cancellationToken = default);
    Task<PayrollEntry?> GetPayrollEntryByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PayrollEntry>> GetPayrollEntriesByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Block C overlap guard (HR-06 live provability): true when a non-Cancelled entry of the
    /// company overlaps [startDate, EndDate] (both bounds inclusive). Cancelled runs released
    /// their accrual (mirror) and their period, so they never block a re-run.
    /// </summary>
    Task<bool> HasOverlappingEntryAsync(Guid companyId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the mutated entry (status transitions, totals, voucher links). Translates EF's
    /// <c>DbUpdateConcurrencyException</c> (RowVersion WHERE clause matched 0 rows) into
    /// <see cref="Exceptions.ConcurrencyConflictException"/>, mirroring the manufacturing repository.
    /// </summary>
    Task UpdatePayrollEntryAsync(PayrollEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists one slip. Rejects a second slip for the same (entry, employee) pair with a
    /// typed <c>duplicate_salary_slip</c> failure (spec HR-06 - exactly one slip; the DB unique
    /// index is the authority, this pre-check is 409 UX only).
    /// </summary>
    Task AddSlipAsync(SalarySlip slip, CancellationToken cancellationToken = default);
    Task AddSlipLinesAsync(IReadOnlyList<SalarySlipLine> lines, CancellationToken cancellationToken = default);
    Task<bool> SlipExistsAsync(Guid payrollEntryId, Guid employeeId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SalarySlip>> GetSlipsByEntryAsync(Guid payrollEntryId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SalarySlipLine>> GetSlipLinesByEntryAsync(Guid payrollEntryId, CancellationToken cancellationToken = default);

    /// <summary>Saves the mutated slip (Submitted/Cancelled transitions). Same concurrency translation as <see cref="UpdatePayrollEntryAsync"/>.</summary>
    Task UpdateSlipAsync(SalarySlip slip, CancellationToken cancellationToken = default);

    /// <summary>Appends GL lines (inside the ambient posting transaction). GLEntry stays INSERT-ONLY.</summary>
    Task AddGlEntriesAsync(IReadOnlyList<GLEntry> glEntries, CancellationToken cancellationToken = default);

    /// <summary>Live accrual lines of one entry (VoucherType "Payroll", VoucherId = entry id), ordered by id - the cancel-mirror source.</summary>
    Task<IReadOnlyList<GLEntry>> GetAccrualGlEntriesAsync(Guid payrollEntryId, CancellationToken cancellationToken = default);
}
