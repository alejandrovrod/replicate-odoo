using Erp.Domain.Entities;
using Erp.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IHrPayrollRepository"/>. The department ancestor walk
/// mirrors WarehouseRepository.GetByIdWithAncestorsAsync (cycle prevention for the tree);
/// the structure read includes its lines (aggregate read). CompanyId predicates are business
/// scoping, not tenancy (Constitution II.3 stays automatic).
/// </summary>
public sealed class HrPayrollRepository : IHrPayrollRepository
{
    private readonly AppDbContext _dbContext;

    public HrPayrollRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        // Join an already-open transaction instead of creating a nested one (same DbContext instance).
        if (_dbContext.Database.CurrentTransaction is not null)
        {
            return await operation(cancellationToken);
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var result = await operation(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    // Departments

    public async Task AddDepartmentAsync(Department department, CancellationToken cancellationToken = default)
    {
        await _dbContext.Departments.AddAsync(department, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<Department?> GetDepartmentByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.Departments.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Department>> GetDepartmentWithAncestorsAsync(Guid departmentId, CancellationToken cancellationToken = default)
    {
        var chain = new List<Department>();
        var visited = new HashSet<Guid>();
        Guid? nextId = departmentId;

        while (nextId is { } id)
        {
            if (visited.Contains(id))
            {
                // The stored parent chain loops back on itself (data corruption). Append the
                // repeated node so DepartmentValidator.EnsureNoCycle sees the duplicate and fails
                // the request instead of walking forever (same shape as WarehouseRepository).
                var repeated = chain.First(d => d.Id == id);
                chain.Add(repeated);
                break;
            }

            visited.Add(id);

            var current = await _dbContext.Departments.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
            if (current is null)
            {
                break;
            }

            chain.Add(current);
            nextId = current.ParentDepartmentId;
        }

        return chain;
    }

    public async Task<IReadOnlyList<Department>> GetDepartmentsByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
        => await _dbContext.Departments
            .Where(d => d.CompanyId == companyId)
            .ToListAsync(cancellationToken);

    // Designations

    public async Task AddDesignationAsync(Designation designation, CancellationToken cancellationToken = default)
    {
        await _dbContext.Designations.AddAsync(designation, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<Designation?> GetDesignationByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.Designations.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Designation>> GetDesignationsByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
        => await _dbContext.Designations
            .Where(d => d.CompanyId == companyId)
            .ToListAsync(cancellationToken);

    // Employees

    public async Task AddEmployeeAsync(Employee employee, CancellationToken cancellationToken = default)
    {
        await _dbContext.Employees.AddAsync(employee, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateEmployeeAsync(Employee employee, CancellationToken cancellationToken = default)
    {
        _dbContext.Employees.Update(employee);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<Employee?> GetEmployeeByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.Employees.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public Task<Employee?> GetEmployeeByNumberAsync(Guid companyId, string employeeNumber, CancellationToken cancellationToken = default)
        => _dbContext.Employees.FirstOrDefaultAsync(
            e => e.CompanyId == companyId && e.EmployeeNumber == employeeNumber,
            cancellationToken);

    public async Task<IReadOnlyList<Employee>> GetEmployeesByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
        => await _dbContext.Employees
            .Where(e => e.CompanyId == companyId)
            .ToListAsync(cancellationToken);

    // Salary components

    public async Task AddComponentAsync(SalaryComponent component, CancellationToken cancellationToken = default)
    {
        await _dbContext.SalaryComponents.AddAsync(component, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<SalaryComponent?> GetComponentByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.SalaryComponents.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<SalaryComponent>> GetComponentsByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
        => await _dbContext.SalaryComponents
            .Where(c => c.CompanyId == companyId)
            .ToListAsync(cancellationToken);

    // Salary structures + lines

    public async Task AddStructureAsync(SalaryStructure structure, CancellationToken cancellationToken = default)
    {
        await _dbContext.SalaryStructures.AddAsync(structure, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<SalaryStructure?> GetStructureByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.SalaryStructures
            .Include(s => s.Lines)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<IReadOnlyList<SalaryStructure>> GetStructuresByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
        => await _dbContext.SalaryStructures
            .Include(s => s.Lines)
            .Where(s => s.CompanyId == companyId)
            .ToListAsync(cancellationToken);

    // Assignments

    public async Task AddAssignmentAsync(SalaryStructureAssignment assignment, CancellationToken cancellationToken = default)
    {
        await _dbContext.SalaryStructureAssignments.AddAsync(assignment, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SalaryStructureAssignment>> GetAssignmentsByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
        => await _dbContext.SalaryStructureAssignments
            .Where(a => a.EmployeeId == employeeId)
            .ToListAsync(cancellationToken);
}
