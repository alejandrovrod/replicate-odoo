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
}
