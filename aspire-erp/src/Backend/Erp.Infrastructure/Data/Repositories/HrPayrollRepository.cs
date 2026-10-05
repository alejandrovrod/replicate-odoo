using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

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

    public async Task<IReadOnlyList<SalaryStructureAssignment>> GetAssignmentsByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
        => await _dbContext.SalaryStructureAssignments
            .Where(a => a.CompanyId == companyId)
            .ToListAsync(cancellationToken);

    // Payroll batch engine (Tasks 12.3-12.4)

    /// <summary>
    /// Constitution III.4: SELECT MAX(PayrollNumber) WITH (UPDLOCK, HOLDLOCK) inside the AMBIENT
    /// submit transaction, scoped to (TenantId, CompanyId, year). A rolled-back submit consumes
    /// NO number (same generator shape as the manufacturing repository).
    /// </summary>
    public async Task<string> NextPayrollNumberAsync(
        Guid companyId, int year, CancellationToken cancellationToken = default)
        => await NextSequenceAsync(
            "dbo.PayrollEntry", "PayrollNumber", companyId, "PE", year, cancellationToken);

    public async Task<string> NextVoucherNumberAsync(
        Guid companyId, string prefix, int year, CancellationToken cancellationToken = default)
        => await NextGlSequenceAsync(companyId, prefix, year, cancellationToken);

    /// <summary>
    /// HR-06 live race guard: UPDLOCK/HOLDLOCK over the entry's slip rows, held to transaction
    /// end, so concurrent slip inserts for the same entry serialize. The DB unique index stays
    /// the authority (a blocked rival fails on it, never double-inserts).
    /// </summary>
    public async Task LockEntrySlipsAsync(Guid payrollEntryId, CancellationToken cancellationToken = default)
    {
        var transaction = _dbContext.Database.CurrentTransaction
            ?? throw new InvalidOperationException(
                "Slip range locking must run inside the posting transaction: "
                + "outside one the UPDLOCK/HOLDLOCK range lock cannot protect the insert.");

        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await _dbContext.Database.OpenConnectionAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT 1 FROM dbo.SalarySlip WITH (UPDLOCK, HOLDLOCK) WHERE PayrollEntryId = @EntryId;";
        command.Transaction = transaction.GetDbTransaction();

        var parameter = command.CreateParameter();
        parameter.ParameterName = "@EntryId";
        parameter.Value = payrollEntryId;
        command.Parameters.Add(parameter);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task AddPayrollEntryAsync(PayrollEntry entry, CancellationToken cancellationToken = default)
    {
        await _dbContext.PayrollEntries.AddAsync(entry, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<PayrollEntry?> GetPayrollEntryByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.PayrollEntries
            .Include(e => e.Slips)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    /// <summary>
    /// Block C overlap guard read: any non-Cancelled entry of the company whose window
    /// intersects [startDate, endDate] blocks the submit. Runs INSIDE the ambient submit
    /// transaction AFTER <see cref="NextPayrollNumberAsync"/> took its UPDLOCK/HOLDLOCK over
    /// the company's year range, so concurrent same-year submits serialize on the numbering
    /// lock first and the loser observes the winner's committed entry (409, zero writes).
    /// Status is stored as its NAME (HasConversion&lt;string&gt;), so the enum comparison
    /// below translates to a string inequality.
    /// </summary>
    public Task<bool> HasOverlappingEntryAsync(Guid companyId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
        => _dbContext.PayrollEntries
            .AnyAsync(
                e => e.CompanyId == companyId
                    && e.Status != PayrollEntryStatus.Cancelled
                    && e.StartDate <= endDate
                    && e.EndDate >= startDate,
                cancellationToken);

    public async Task<IReadOnlyList<PayrollEntry>> GetPayrollEntriesByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
        => await _dbContext.PayrollEntries
            .Where(e => e.CompanyId == companyId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task UpdatePayrollEntryAsync(PayrollEntry entry, CancellationToken cancellationToken = default)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(nameof(PayrollEntry), entry.Id, ex);
        }
    }

    public async Task AddSlipAsync(SalarySlip slip, CancellationToken cancellationToken = default)
    {
        var exists = await _dbContext.SalarySlips
            .AnyAsync(s => s.PayrollEntryId == slip.PayrollEntryId && s.EmployeeId == slip.EmployeeId, cancellationToken);

        if (exists)
        {
            // Spec HR-06: exactly one slip per (entry, employee). The DB unique index
            // UQ_SalarySlip_Entry_Employee is the authority; this pre-check is 409 UX only.
            throw new HrValidationException(
                HrPayrollErrorCodes.DuplicateSalarySlip,
                $"A salary slip for employee '{slip.EmployeeId}' already exists in payroll entry '{slip.PayrollEntryId}'.");
        }

        await _dbContext.SalarySlips.AddAsync(slip, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Persists slip lines. Lines that <see cref="AddSlipAsync"/> already cascade-saved through
    /// the slip's <c>Lines</c> navigation arrive here tracked (Unchanged) and are SKIPPED -
    /// re-adding them would insert every row a second time (live Block C proof: PK violation
    /// on the second save). Lines that are still unsaved (Added) or untracked (Detached) are
    /// inserted normally, so the method stays correct when called without the cascade.
    /// </summary>
    public async Task AddSlipLinesAsync(IReadOnlyList<SalarySlipLine> lines, CancellationToken cancellationToken = default)
    {
        var pending = lines
            .Where(l =>
            {
                var state = _dbContext.Entry(l).State;
                return state is EntityState.Detached or EntityState.Added;
            })
            .ToList();

        if (pending.Count == 0)
        {
            return;
        }

        _dbContext.SalarySlipLines.AddRange(pending);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> SlipExistsAsync(Guid payrollEntryId, Guid employeeId, CancellationToken cancellationToken = default)
        => _dbContext.SalarySlips
            .AnyAsync(s => s.PayrollEntryId == payrollEntryId && s.EmployeeId == employeeId, cancellationToken);

    public async Task<IReadOnlyList<SalarySlip>> GetSlipsByEntryAsync(Guid payrollEntryId, CancellationToken cancellationToken = default)
        => await _dbContext.SalarySlips
            .Include(s => s.Lines)
            .Where(s => s.PayrollEntryId == payrollEntryId)
            .OrderBy(s => s.SlipNumber)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SalarySlipLine>> GetSlipLinesByEntryAsync(Guid payrollEntryId, CancellationToken cancellationToken = default)
        => await _dbContext.SalarySlipLines
            .Where(l => l.Slip != null && l.Slip.PayrollEntryId == payrollEntryId)
            .OrderBy(l => l.Slip!.SlipNumber)
            .ThenBy(l => l.ComponentName)
            .ToListAsync(cancellationToken);

    public async Task UpdateSlipAsync(SalarySlip slip, CancellationToken cancellationToken = default)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(nameof(SalarySlip), slip.Id, ex);
        }
    }

    public async Task AddGlEntriesAsync(IReadOnlyList<GLEntry> glEntries, CancellationToken cancellationToken = default)
    {
        _dbContext.GLEntries.AddRange(glEntries);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GLEntry>> GetAccrualGlEntriesAsync(Guid payrollEntryId, CancellationToken cancellationToken = default)
        => await _dbContext.GLEntries
            .Where(g => g.VoucherId == payrollEntryId && g.VoucherType == PayrollVoucherType && !g.IsCancelled)
            .OrderBy(g => g.Id)
            .ToListAsync(cancellationToken);

    private const string PayrollVoucherType = "Payroll";

    private async Task<string> NextSequenceAsync(
        string table, string column, Guid companyId, string prefix, int year, CancellationToken cancellationToken)
    {
        var transaction = _dbContext.Database.CurrentTransaction
            ?? throw new InvalidOperationException(
                "Payroll numbering must run inside the posting transaction (Constitution III.4): "
                + "outside one the UPDLOCK/HOLDLOCK range lock cannot protect the sequence.");

        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await _dbContext.Database.OpenConnectionAsync(cancellationToken);
        }

        var tenantId = _dbContext.CurrentTenantId;
        var pattern = $"{prefix}-{year}-%";

        await using var command = connection.CreateCommand();
        command.CommandText =
            $"SELECT MAX({column}) FROM {table} WITH (UPDLOCK, HOLDLOCK) "
            + $"WHERE TenantId = @TenantId AND CompanyId = @CompanyId AND {column} LIKE @Pattern;";
        command.Transaction = transaction.GetDbTransaction();

        AddParameter(command, "@TenantId", tenantId);
        AddParameter(command, "@CompanyId", companyId);
        AddParameter(command, "@Pattern", pattern);

        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        return FormatSequence(prefix, year, scalar as string);
    }

    private async Task<string> NextGlSequenceAsync(
        Guid companyId, string prefix, int year, CancellationToken cancellationToken)
    {
        var transaction = _dbContext.Database.CurrentTransaction
            ?? throw new InvalidOperationException(
                "Voucher numbering must run inside the posting transaction (Constitution III.4): "
                + "outside one the UPDLOCK/HOLDLOCK range lock cannot protect the sequence.");

        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await _dbContext.Database.OpenConnectionAsync(cancellationToken);
        }

        var tenantId = _dbContext.CurrentTenantId;
        var pattern = $"{prefix}-{year}-%";

        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT MAX(VoucherNo) FROM dbo.GLEntry WITH (UPDLOCK, HOLDLOCK) "
            + "WHERE TenantId = @TenantId AND CompanyId = @CompanyId AND VoucherNo LIKE @Pattern;";
        command.Transaction = transaction.GetDbTransaction();

        AddParameter(command, "@TenantId", tenantId);
        AddParameter(command, "@CompanyId", companyId);
        AddParameter(command, "@Pattern", pattern);

        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        return FormatSequence(prefix, year, scalar as string);
    }

    private static string FormatSequence(string prefix, int year, string? max)
    {
        var nextSequence = 1;
        if (!string.IsNullOrEmpty(max))
        {
            var separator = max.LastIndexOf('-');
            if (separator < 0 || !int.TryParse(max[(separator + 1)..], out var currentSequence))
            {
                throw new InvalidOperationException(
                    $"Stored voucher number '{max}' does not follow the PREFIX-YYYY-NNNNN format.");
            }

            if (currentSequence >= 99999)
            {
                throw new InvalidOperationException(
                    $"Voucher sequence for '{prefix}-{year}' is exhausted (max 99999).");
            }

            nextSequence = currentSequence + 1;
        }

        return $"{prefix}-{year}-{nextSequence:D5}";
    }

    private static void AddParameter(System.Data.Common.DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
