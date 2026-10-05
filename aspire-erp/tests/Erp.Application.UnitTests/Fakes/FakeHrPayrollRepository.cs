using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.UnitTests.Fakes;

/// <summary>
/// In-memory <see cref="IHrPayrollRepository"/>: masters come from seeded lists, the
/// transaction simply executes its callback, and every persisted aggregate is captured so
/// tests can assert on components / structures / assignments without a database.
/// </summary>
public sealed class FakeHrPayrollRepository : IHrPayrollRepository
{
    private readonly List<Department> _departments = new();
    private readonly List<Designation> _designations = new();
    private readonly List<Employee> _employees = new();
    private readonly List<SalaryComponent> _components = new();
    private readonly List<SalaryStructure> _structures = new();
    private readonly List<SalaryStructureAssignment> _assignments = new();

    public SalaryComponent? AddedComponent { get; private set; }

    public SalaryStructure? AddedStructure { get; private set; }

    public SalaryStructureAssignment? AddedAssignment { get; private set; }

    /// <summary>Number of transactions opened (proves multi-row writes run inside ONE transaction).</summary>
    public int TransactionCount { get; private set; }

    public void Seed(params SalaryComponent[] components) => _components.AddRange(components);

    public void Seed(params Employee[] employees) => _employees.AddRange(employees);

    public void Seed(params SalaryStructure[] structures) => _structures.AddRange(structures);

    public void Seed(params SalaryStructureAssignment[] assignments) => _assignments.AddRange(assignments);

    public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        TransactionCount++;
        return operation(cancellationToken);
    }

    public Task AddDepartmentAsync(Department department, CancellationToken cancellationToken = default)
    {
        _departments.Add(department);
        return Task.CompletedTask;
    }

    public Task<Department?> GetDepartmentByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_departments.FirstOrDefault(d => d.Id == id));

    public Task<IReadOnlyList<Department>> GetDepartmentWithAncestorsAsync(Guid departmentId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Department>>(Array.Empty<Department>());

    public Task<IReadOnlyList<Department>> GetDepartmentsByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Department>>(_departments.Where(d => d.CompanyId == companyId).ToList());

    public Task AddDesignationAsync(Designation designation, CancellationToken cancellationToken = default)
    {
        _designations.Add(designation);
        return Task.CompletedTask;
    }

    public Task<Designation?> GetDesignationByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_designations.FirstOrDefault(d => d.Id == id));

    public Task<IReadOnlyList<Designation>> GetDesignationsByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Designation>>(_designations.Where(d => d.CompanyId == companyId).ToList());

    public Task AddEmployeeAsync(Employee employee, CancellationToken cancellationToken = default)
    {
        _employees.Add(employee);
        return Task.CompletedTask;
    }

    public Task UpdateEmployeeAsync(Employee employee, CancellationToken cancellationToken = default)
    {
        var index = _employees.FindIndex(e => e.Id == employee.Id);
        if (index >= 0)
        {
            _employees[index] = employee;
        }
        else
        {
            _employees.Add(employee);
        }

        return Task.CompletedTask;
    }

    public Task<Employee?> GetEmployeeByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_employees.FirstOrDefault(e => e.Id == id));

    public Task<Employee?> GetEmployeeByNumberAsync(Guid companyId, string employeeNumber, CancellationToken cancellationToken = default)
        => Task.FromResult(_employees.FirstOrDefault(
            e => e.CompanyId == companyId && e.EmployeeNumber == employeeNumber));

    public Task<IReadOnlyList<Employee>> GetEmployeesByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Employee>>(_employees.Where(e => e.CompanyId == companyId).ToList());

    public Task AddComponentAsync(SalaryComponent component, CancellationToken cancellationToken = default)
    {
        AddedComponent = component;
        _components.Add(component);
        return Task.CompletedTask;
    }

    public Task<SalaryComponent?> GetComponentByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_components.FirstOrDefault(c => c.Id == id));

    public Task<IReadOnlyList<SalaryComponent>> GetComponentsByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<SalaryComponent>>(_components.Where(c => c.CompanyId == companyId).ToList());

    public Task AddStructureAsync(SalaryStructure structure, CancellationToken cancellationToken = default)
    {
        AddedStructure = structure;
        _structures.Add(structure);
        return Task.CompletedTask;
    }

    public Task<SalaryStructure?> GetStructureByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_structures.FirstOrDefault(s => s.Id == id));

    public Task<IReadOnlyList<SalaryStructure>> GetStructuresByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<SalaryStructure>>(_structures.Where(s => s.CompanyId == companyId).ToList());

    public Task AddAssignmentAsync(SalaryStructureAssignment assignment, CancellationToken cancellationToken = default)
    {
        AddedAssignment = assignment;
        _assignments.Add(assignment);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SalaryStructureAssignment>> GetAssignmentsByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<SalaryStructureAssignment>>(_assignments.Where(a => a.EmployeeId == employeeId).ToList());
}
