using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.UnitTests.Fakes;

/// <summary>
/// In-memory <see cref="IHrPayrollRepository"/>: masters come from seeded lists, the
/// transaction simply executes its callback, and every persisted aggregate is captured so
/// tests can assert on components / structures / assignments without a database. Block B
/// extends it with the batch engine: entries, slips, slip lines and GL captures behind the
/// same seam, gapless PE/PYR sequences and the HR-06 duplicate-slip guard.
/// </summary>
/// <remarks>
/// Rollback simulation (the FakeAssetsRepository snapshot pattern): the transaction callback
/// mutates shared in-memory instances eagerly, so on ANY exception the pre-call snapshot -
/// entry fields, slip statuses, added-row counts - is restored before rethrowing, exactly
/// like the real repository abandoning its uncommitted transaction. Optimistic-concurrency
/// races are simulated with <see cref="FailNextEntryUpdate"/> / <see cref="FailNextSlipUpdate"/>
/// (the next save throws <see cref="ConcurrencyConflictException"/> like the real repository
/// does after a RowVersion mismatch, then the flag resets so only one call fails).
/// </remarks>
public sealed class FakeHrPayrollRepository : IHrPayrollRepository
{
    private readonly List<Department> _departments = new();
    private readonly List<Designation> _designations = new();
    private readonly List<Employee> _employees = new();
    private readonly List<SalaryComponent> _components = new();
    private readonly List<SalaryStructure> _structures = new();
    private readonly List<SalaryStructureAssignment> _assignments = new();
    private readonly List<PayrollEntry> _entries = new();
    private readonly List<SalarySlip> _slips = new();
    private readonly List<SalarySlipLine> _slipLines = new();
    private readonly List<GLEntry> _addedGl = new();
    private readonly Dictionary<(Guid CompanyId, int Year), int> _payrollSequences = new();
    private readonly Dictionary<(Guid CompanyId, string Prefix, int Year), int> _voucherSequences = new();

    public SalaryComponent? AddedComponent { get; private set; }

    public SalaryStructure? AddedStructure { get; private set; }

    public SalaryStructureAssignment? AddedAssignment { get; private set; }

    /// <summary>Number of transactions opened (proves multi-row writes run inside ONE transaction).</summary>
    public int TransactionCount { get; private set; }

    /// <summary>All payroll entries (seeded + submitted).</summary>
    public IReadOnlyList<PayrollEntry> Entries => _entries;

    /// <summary>All salary slips (seeded + generated).</summary>
    public IReadOnlyList<SalarySlip> Slips => _slips;

    /// <summary>All slip lines (seeded + generated).</summary>
    public IReadOnlyList<SalarySlipLine> SlipLines => _slipLines;

    /// <summary>All GL lines appended by the batch engine (accrual, disbursement, mirrors).</summary>
    public IReadOnlyList<GLEntry> AddedGlEntries => _addedGl;

    /// <summary>
    /// When set, the NEXT <c>UpdatePayrollEntryAsync</c> fails like the real repository does after
    /// a RowVersion mismatch; the flag resets itself so only one call fails.
    /// </summary>
    public bool FailNextEntryUpdate { get; set; }

    /// <summary>
    /// When set, the NEXT <c>UpdateSlipAsync</c> fails like the real repository does after
    /// a RowVersion mismatch; the flag resets itself so only one call fails.
    /// </summary>
    public bool FailNextSlipUpdate { get; set; }

    public void SeedEntry(params PayrollEntry[] entries) => _entries.AddRange(entries);

    public void SeedSlip(params SalarySlip[] slips) => _slips.AddRange(slips);

    public void SeedSlipLines(params SalarySlipLine[] lines) => _slipLines.AddRange(lines);

    /// <summary>Test helper: directly adds GL entries for multi-stage test setup.</summary>
    public void AddGlEntriesDirect(IEnumerable<GLEntry> lines) => _addedGl.AddRange(lines);

    public void Seed(params SalaryComponent[] components) => _components.AddRange(components);

    public void Seed(params Employee[] employees) => _employees.AddRange(employees);

    public void Seed(params SalaryStructure[] structures) => _structures.AddRange(structures);

    public void Seed(params SalaryStructureAssignment[] assignments) => _assignments.AddRange(assignments);

    public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        TransactionCount++;

        // The real repository rolls the whole transaction back when the operation throws; the
        // in-memory fake models that for the mutations the 12.3/12.4 handlers perform BEFORE a
        // gate or CAS failure (entry fields, slip statuses, added entry/slip/line/GL rows), so
        // rejection tests can assert the books are untouched.
        var entrySnapshot = _entries
            .Select(e => (Entry: e, e.Status, e.TotalGrossPay, e.TotalDeductions, e.TotalNetPay,
                e.PayrollNumber, e.AccrualVoucherNo, e.PaymentVoucherNo))
            .ToList();
        var slipSnapshot = _slips
            .Select(s => (Slip: s, s.Status, s.SlipNumber))
            .ToList();
        var entryCount = _entries.Count;
        var slipCount = _slips.Count;
        var lineCount = _slipLines.Count;
        var glCount = _addedGl.Count;

        try
        {
            return await operation(cancellationToken);
        }
        catch
        {
            foreach (var (entry, status, gross, ded, net, number, accrual, payment) in entrySnapshot)
            {
                entry.Status = status;
                entry.TotalGrossPay = gross;
                entry.TotalDeductions = ded;
                entry.TotalNetPay = net;
                entry.PayrollNumber = number;
                entry.AccrualVoucherNo = accrual;
                entry.PaymentVoucherNo = payment;
            }

            foreach (var (slip, status, number) in slipSnapshot)
            {
                slip.Status = status;
                slip.SlipNumber = number;
            }

            while (_entries.Count > entryCount)
            {
                _entries.RemoveAt(_entries.Count - 1);
            }

            while (_slips.Count > slipCount)
            {
                _slips.RemoveAt(_slips.Count - 1);
            }

            while (_slipLines.Count > lineCount)
            {
                _slipLines.RemoveAt(_slipLines.Count - 1);
            }

            while (_addedGl.Count > glCount)
            {
                _addedGl.RemoveAt(_addedGl.Count - 1);
            }

            throw;
        }
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

    public Task<IReadOnlyList<SalaryStructureAssignment>> GetAssignmentsByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<SalaryStructureAssignment>>(_assignments.Where(a => a.CompanyId == companyId).ToList());

    public Task<string> NextPayrollNumberAsync(Guid companyId, int year, CancellationToken cancellationToken = default)
    {
        var key = (companyId, year);
        _payrollSequences.TryGetValue(key, out var current);
        _payrollSequences[key] = current + 1;
        return Task.FromResult($"PE-{year}-{current + 1:D5}");
    }

    public Task<string> NextVoucherNumberAsync(
        Guid companyId,
        string prefix,
        int year,
        CancellationToken cancellationToken = default)
    {
        var key = (companyId, prefix, year);
        _voucherSequences.TryGetValue(key, out var current);
        _voucherSequences[key] = current + 1;
        return Task.FromResult($"{prefix}-{year}-{current + 1:D5}");
    }

    public Task LockEntrySlipsAsync(Guid payrollEntryId, CancellationToken cancellationToken = default)
    {
        // In-memory: no concurrent writers exist; the HR-06 range lock is a no-op here and is
        // proven live in Block C. The duplicate-slip guard below is the unit-testable half.
        return Task.CompletedTask;
    }

    public Task AddPayrollEntryAsync(PayrollEntry entry, CancellationToken cancellationToken = default)
    {
        _entries.Add(entry);
        return Task.CompletedTask;
    }

    public Task<PayrollEntry?> GetPayrollEntryByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_entries.FirstOrDefault(e => e.Id == id));

    public Task<bool> HasOverlappingEntryAsync(Guid companyId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
        => Task.FromResult(_entries.Any(
            e => e.CompanyId == companyId
                && e.Status != PayrollEntryStatus.Cancelled
                && e.StartDate <= endDate
                && e.EndDate >= startDate));

    public Task<IReadOnlyList<PayrollEntry>> GetPayrollEntriesByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PayrollEntry>>(
            _entries.Where(e => e.CompanyId == companyId).OrderByDescending(e => e.CreatedAt).ToList());

    public Task UpdatePayrollEntryAsync(PayrollEntry entry, CancellationToken cancellationToken = default)
    {
        if (FailNextEntryUpdate)
        {
            FailNextEntryUpdate = false;
            throw new ConcurrencyConflictException(nameof(PayrollEntry), entry.Id);
        }

        // In-memory: the entity instance IS the store; transitions are already applied.
        return Task.CompletedTask;
    }

    public Task AddSlipAsync(SalarySlip slip, CancellationToken cancellationToken = default)
    {
        if (_slips.Any(s => s.PayrollEntryId == slip.PayrollEntryId && s.EmployeeId == slip.EmployeeId))
        {
            throw new HrValidationException(
                HrPayrollErrorCodes.DuplicateSalarySlip,
                $"A salary slip for employee '{slip.EmployeeId}' already exists in payroll entry '{slip.PayrollEntryId}'.");
        }

        _slips.Add(slip);
        return Task.CompletedTask;
    }

    public Task AddSlipLinesAsync(IReadOnlyList<SalarySlipLine> lines, CancellationToken cancellationToken = default)
    {
        _slipLines.AddRange(lines);
        return Task.CompletedTask;
    }

    public Task<bool> SlipExistsAsync(Guid payrollEntryId, Guid employeeId, CancellationToken cancellationToken = default)
        => Task.FromResult(_slips.Any(s => s.PayrollEntryId == payrollEntryId && s.EmployeeId == employeeId));

    public Task<IReadOnlyList<SalarySlip>> GetSlipsByEntryAsync(Guid payrollEntryId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<SalarySlip>>(
            _slips.Where(s => s.PayrollEntryId == payrollEntryId).OrderBy(s => s.SlipNumber).ToList());

    public Task<IReadOnlyList<SalarySlipLine>> GetSlipLinesByEntryAsync(Guid payrollEntryId, CancellationToken cancellationToken = default)
    {
        var slipNumbers = _slips.Where(s => s.PayrollEntryId == payrollEntryId).ToDictionary(s => s.Id, s => s.SlipNumber);
        return Task.FromResult<IReadOnlyList<SalarySlipLine>>(
            _slipLines.Where(l => slipNumbers.ContainsKey(l.SlipId)).OrderBy(l => l.ComponentName).ToList());
    }

    public Task UpdateSlipAsync(SalarySlip slip, CancellationToken cancellationToken = default)
    {
        if (FailNextSlipUpdate)
        {
            FailNextSlipUpdate = false;
            throw new ConcurrencyConflictException(nameof(SalarySlip), slip.Id);
        }

        // In-memory: the entity instance IS the store; transitions are already applied.
        return Task.CompletedTask;
    }

    public Task AddGlEntriesAsync(IReadOnlyList<GLEntry> glEntries, CancellationToken cancellationToken = default)
    {
        _addedGl.AddRange(glEntries);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<GLEntry>> GetAccrualGlEntriesAsync(Guid payrollEntryId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<GLEntry>>(
            _addedGl.Where(g => g.VoucherId == payrollEntryId && g.VoucherType == "Payroll" && !g.IsCancelled)
                .OrderBy(g => g.Id).ToList());
}
