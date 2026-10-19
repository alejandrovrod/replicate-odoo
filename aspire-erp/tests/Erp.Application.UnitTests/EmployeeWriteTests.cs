using Erp.Application.Features.HrPayroll.Commands;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Employee register/edit acceptance (spec: HR staff lifecycle) through the CQRS handlers against
/// in-memory repository doubles: identity + uniqueness, the bank/dates field rules, the atomic
/// initial structure assignment, the "rejected creation writes ZERO rows" guarantee and the
/// rowversion optimistic-concurrency gate on update.
/// </summary>
public sealed class EmployeeWriteTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly FakeCompanyRepository _companies = new();
    private readonly FakeHrPayrollRepository _hr = new();

    public EmployeeWriteTests()
    {
        _companies.Company = new Company
        {
            Id = _companyId,
            TenantId = Guid.NewGuid(),
            Name = "Acme Industrial",
            TaxId = "ACME-0001",
            CountryCode = "MX",
        };
    }

    private CreateEmployeeCommandHandler CreateHandler() => new(_companies, _hr);

    private UpdateEmployeeCommandHandler UpdateHandler() => new(_hr);

    private static CreateEmployeeCommand CreateCommand(
        Guid companyId,
        string employeeNumber = "EMP-0001",
        string workEmail = "ana.reyes@example.com",
        DateOnly? joining = null,
        DateOnly? relieving = null,
        SalaryMode salaryMode = SalaryMode.Bank,
        string? bankName = "Acme Bank",
        string? bankAccountNumber = "000123456789",
        EmploymentStatus status = EmploymentStatus.Active,
        Guid? salaryStructureId = null) =>
        new(
            companyId,
            employeeNumber,
            "Ana",
            "Reyes",
            workEmail,
            joining ?? new DateOnly(2025, 3, 1),
            relieving,
            salaryMode,
            bankName,
            bankAccountNumber,
            status,
            salaryStructureId);

    private static UpdateEmployeeCommand UpdateCommand(
        Guid companyId,
        Guid employeeId,
        string employeeNumber = "EMP-0001",
        DateOnly? joining = null,
        DateOnly? relieving = null,
        SalaryMode salaryMode = SalaryMode.Bank,
        string? bankName = "Acme Bank",
        string? bankAccountNumber = "000123456789",
        EmploymentStatus status = EmploymentStatus.Active,
        byte[]? rowVersion = null) =>
        new(
            companyId,
            employeeId,
            employeeNumber,
            "Ana",
            "Reyes",
            "ana.reyes@example.com",
            joining ?? new DateOnly(2025, 3, 1),
            relieving,
            salaryMode,
            bankName,
            bankAccountNumber,
            status,
            rowVersion);

    private Employee SeedEmployee(
        string employeeNumber = "EMP-0001",
        byte[]? rowVersion = null,
        Guid? companyId = null) 
    {
        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId ?? _companyId,
            EmployeeNumber = employeeNumber,
            FirstName = "Ana",
            LastName = "Reyes",
            WorkEmail = "ana.reyes@example.com",
            DateOfJoining = new DateOnly(2025, 3, 1),
            SalaryMode = SalaryMode.Bank,
            BankName = "Acme Bank",
            BankAccountNumber = "000123456789",
            Status = EmploymentStatus.Active,
            IsActive = true,
            RowVersion = rowVersion ?? new byte[] { 1, 2, 3, 4 },
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _hr.Seed(employee);
        return employee;
    }

    private SalaryStructure SeedStructure(bool isActive = true, Guid? companyId = null)
    {
        var structure = new SalaryStructure
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId ?? _companyId,
            StructureName = "Monthly Standard",
            IsActive = isActive,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _hr.Seed(structure);
        return structure;
    }

    [Fact]
    public async Task Create_persists_the_employee_without_assignment_when_no_structure_is_given()
    {
        var result = await CreateHandler().HandleAsync(CreateCommand(_companyId));

        Assert.True(result.IsSuccess);
        Assert.Null(_hr.AddedAssignment);

        var stored = await _hr.GetEmployeeByNumberAsync(_companyId, "EMP-0001");
        Assert.NotNull(stored);
        Assert.Equal("Ana", stored!.FirstName);
        Assert.Equal(EmploymentStatus.Active, stored.Status);
        Assert.True(stored.IsActive);
    }

    [Fact]
    public async Task Create_creates_the_initial_assignment_from_the_joining_date()
    {
        var structure = SeedStructure();

        var result = await CreateHandler().HandleAsync(
            CreateCommand(_companyId, salaryStructureId: structure.Id));

        Assert.True(result.IsSuccess);
        var assignment = Assert.IsType<SalaryStructureAssignment>(_hr.AddedAssignment);
        Assert.Equal(structure.Id, assignment.StructureId);
        Assert.Equal(result.Value!.Id, assignment.EmployeeId);
        Assert.Equal(new DateOnly(2025, 3, 1), assignment.EffectiveFrom);
        Assert.Null(assignment.EffectiveTo);
        Assert.True(assignment.IsActive);
    }

    [Fact]
    public async Task Create_rejects_a_duplicate_employee_number_and_writes_nothing()
    {
        SeedEmployee();

        var result = await CreateHandler().HandleAsync(CreateCommand(_companyId));

        Assert.False(result.IsSuccess);
        Assert.Equal(HrPayrollErrorCodes.DuplicateEmployeeNumber, result.Error!.Code);
        Assert.Equal(0, _hr.TransactionCount);
    }

    [Fact]
    public async Task Create_rejects_bank_pay_without_bank_details_and_writes_nothing()
    {
        var result = await CreateHandler().HandleAsync(
            CreateCommand(_companyId, salaryMode: SalaryMode.Bank, bankName: null, bankAccountNumber: null));

        Assert.False(result.IsSuccess);
        Assert.Equal(HrPayrollErrorCodes.BankNameRequired, result.Error!.Code);
        Assert.Equal(0, _hr.TransactionCount);
    }

    [Fact]
    public async Task Create_allows_cash_pay_without_bank_details()
    {
        var result = await CreateHandler().HandleAsync(
            CreateCommand(_companyId, salaryMode: SalaryMode.Cash, bankName: null, bankAccountNumber: null));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Create_rejects_relieving_before_joining_and_writes_nothing()
    {
        var result = await CreateHandler().HandleAsync(
            CreateCommand(
                _companyId,
                joining: new DateOnly(2025, 3, 1),
                relieving: new DateOnly(2025, 2, 1)));

        Assert.False(result.IsSuccess);
        Assert.Equal(HrPayrollErrorCodes.RelievingBeforeJoining, result.Error!.Code);
        Assert.Equal(0, _hr.TransactionCount);
    }

    [Fact]
    public async Task Create_rejects_an_empty_or_malformed_work_email()
    {
        var result = await CreateHandler().HandleAsync(
            CreateCommand(_companyId, workEmail: "not-an-email"));

        Assert.False(result.IsSuccess);
        Assert.Equal(HrPayrollErrorCodes.InvalidEmployeeEmail, result.Error!.Code);
        Assert.Equal(0, _hr.TransactionCount);
    }

    [Fact]
    public async Task Create_rejects_an_inactive_salary_structure()
    {
        var structure = SeedStructure(isActive: false);

        var result = await CreateHandler().HandleAsync(
            CreateCommand(_companyId, salaryStructureId: structure.Id));

        Assert.False(result.IsSuccess);
        Assert.Equal(HrPayrollErrorCodes.InactiveStructure, result.Error!.Code);
        Assert.Equal(0, _hr.TransactionCount);
    }

    [Fact]
    public async Task Create_rejects_a_salary_structure_from_another_company()
    {
        var structure = SeedStructure(companyId: Guid.NewGuid());

        var result = await CreateHandler().HandleAsync(
            CreateCommand(_companyId, salaryStructureId: structure.Id));

        Assert.False(result.IsSuccess);
        Assert.Equal(HrPayrollErrorCodes.StructureNotFound, result.Error!.Code);
        Assert.Equal(0, _hr.TransactionCount);
    }

    [Fact]
    public async Task Create_rejects_an_unknown_company()
    {
        _companies.Company = null;

        var result = await CreateHandler().HandleAsync(CreateCommand(_companyId));

        Assert.False(result.IsSuccess);
        Assert.Equal(HrPayrollErrorCodes.CompanyNotFound, result.Error!.Code);
        Assert.Equal(0, _hr.TransactionCount);
    }

    [Fact]
    public async Task Update_applies_the_editable_fields()
    {
        var employee = SeedEmployee(rowVersion: new byte[] { 9, 9, 9 });

        var result = await UpdateHandler().HandleAsync(
            UpdateCommand(
                _companyId,
                employee.Id,
                employeeNumber: "EMP-0002",
                status: EmploymentStatus.Suspended,
                salaryMode: SalaryMode.Cash,
                bankName: null,
                bankAccountNumber: null,
                rowVersion: new byte[] { 9, 9, 9 }));

        Assert.True(result.IsSuccess);
        var stored = await _hr.GetEmployeeByIdAsync(employee.Id);
        Assert.NotNull(stored);
        Assert.Equal("EMP-0002", stored!.EmployeeNumber);
        Assert.Equal(EmploymentStatus.Suspended, stored.Status);
        Assert.Equal(SalaryMode.Cash, stored.SalaryMode);
        Assert.Null(stored.BankName);
    }

    [Fact]
    public async Task Update_rejects_a_stale_rowversion_without_touching_the_row()
    {
        var employee = SeedEmployee(rowVersion: new byte[] { 1, 1, 1 });

        var result = await UpdateHandler().HandleAsync(
            UpdateCommand(
                _companyId,
                employee.Id,
                employeeNumber: "EMP-9999",
                rowVersion: new byte[] { 2, 2, 2 }));

        Assert.False(result.IsSuccess);
        Assert.Equal(ConcurrencyErrorCodes.ConcurrencyConflict, result.Error!.Code);

        var stored = await _hr.GetEmployeeByIdAsync(employee.Id);
        Assert.Equal("EMP-0001", stored!.EmployeeNumber);
    }

    [Fact]
    public async Task Update_rejects_an_employee_from_another_company()
    {
        var employee = SeedEmployee(companyId: Guid.NewGuid());

        var result = await UpdateHandler().HandleAsync(
            UpdateCommand(_companyId, employee.Id, rowVersion: employee.RowVersion));

        Assert.False(result.IsSuccess);
        Assert.Equal(HrPayrollErrorCodes.EmployeeNotFound, result.Error!.Code);
    }

    [Fact]
    public async Task Update_rejects_a_number_that_collides_with_another_employee()
    {
        SeedEmployee(employeeNumber: "EMP-0002");
        var employee = SeedEmployee(employeeNumber: "EMP-0001", rowVersion: employeeRowVersion());

        var result = await UpdateHandler().HandleAsync(
            UpdateCommand(_companyId, employee.Id, employeeNumber: "EMP-0002", rowVersion: employeeRowVersion()));

        Assert.False(result.IsSuccess);
        Assert.Equal(HrPayrollErrorCodes.DuplicateEmployeeNumber, result.Error!.Code);

        var stored = await _hr.GetEmployeeByIdAsync(employee.Id);
        Assert.Equal("EMP-0001", stored!.EmployeeNumber);
    }

    [Fact]
    public async Task Update_keeps_the_same_number_when_it_is_unchanged()
    {
        var employee = SeedEmployee(rowVersion: employeeRowVersion());

        var result = await UpdateHandler().HandleAsync(
            UpdateCommand(_companyId, employee.Id, employeeNumber: "EMP-0001", rowVersion: employeeRowVersion()));

        Assert.True(result.IsSuccess);
    }

    private static byte[] employeeRowVersion() => new byte[] { 5, 5, 5 };
}
