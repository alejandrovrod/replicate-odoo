using Erp.Application.Features.HrPayroll.Commands;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Task 12.2 acceptance ("all components map to valid leaf posting accounts") through the
/// CQRS handlers against in-memory repository doubles: component leaf validation (including
/// group-account rejection), structure line math + component guards, assignment overlap, and
/// the guarantee that every rejected creation writes ZERO rows.
/// </summary>
public sealed class SalaryStructureTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _leafAccountId = Guid.NewGuid();
    private readonly Guid _groupAccountId = Guid.NewGuid();

    private readonly FakeCompanyRepository _companies = new();
    private readonly FakeAccountRepository _accounts = new();
    private readonly FakeHrPayrollRepository _hr = new();

    public SalaryStructureTests()
    {
        _companies.Company = new Company
        {
            Id = _companyId,
            TenantId = Guid.NewGuid(),
            Name = "Acme Industrial",
        };

        _accounts.Seed(
            new Account
            {
                Id = _leafAccountId,
                CompanyId = _companyId,
                AccountCode = "5110",
                AccountName = "Salary and Wages Expense",
                IsActive = true,
                IsGroup = false,
            },
            new Account
            {
                Id = _groupAccountId,
                CompanyId = _companyId,
                AccountCode = "5100",
                AccountName = "Payroll (group)",
                IsActive = true,
                IsGroup = true,
            });
    }

    private SalaryComponent SeedComponent(
        string name = "Basic Salary",
        SalaryComponentType type = SalaryComponentType.Earning,
        bool isActive = true,
        Guid? companyId = null) =>
        SeedComponentWithId(Guid.NewGuid(), name, type, isActive, companyId);

    private SalaryComponent SeedComponentWithId(
        Guid id,
        string name,
        SalaryComponentType type,
        bool isActive,
        Guid? companyId = null)
    {
        var component = new SalaryComponent
        {
            Id = id,
            CompanyId = companyId ?? _companyId,
            ComponentName = name,
            ComponentType = type,
            DefaultGLAccountId = _leafAccountId,
            IsActive = isActive,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _hr.Seed(component);
        return component;
    }

    private Employee SeedEmployee(EmploymentStatus status = EmploymentStatus.Active, bool isActive = true) =>
        SeedEmployeeWithId(Guid.NewGuid(), status, isActive);

    private Employee SeedEmployeeWithId(Guid id, EmploymentStatus status, bool isActive)
    {
        var employee = new Employee
        {
            Id = id,
            CompanyId = _companyId,
            EmployeeNumber = $"EMP-{id.ToString()[..8]}",
            FirstName = "Maria",
            LastName = "Santos",
            WorkEmail = "maria.santos@example.com",
            DateOfJoining = new DateOnly(2025, 1, 15),
            Status = status,
            IsActive = isActive,
        };

        _hr.Seed(employee);
        return employee;
    }

    private SalaryStructure SeedStructure(string name = "Standard", bool isActive = true, Guid? companyId = null)
    {
        var structure = new SalaryStructure
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId ?? _companyId,
            StructureName = name,
            IsActive = isActive,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _hr.Seed(structure);
        return structure;
    }

    // Component leaf validation

    [Fact]
    public async Task CreateComponent_LeafAccount_PersistsAndReturnsDto()
    {
        var handler = new CreateSalaryComponentCommandHandler(_companies, _accounts, _hr);

        var result = await handler.HandleAsync(
            new CreateSalaryComponentCommand(
                _companyId, "Basic Salary", SalaryComponentType.Earning, _leafAccountId));

        Assert.True(result.IsSuccess);
        Assert.Equal("Basic Salary", result.Value!.ComponentName);
        Assert.Equal(_leafAccountId, result.Value.DefaultGLAccountId);
        Assert.NotNull(_hr.AddedComponent);
    }

    [Fact]
    public async Task CreateComponent_GroupAccount_FailsWithInvalidGlAccountAndWritesNothing()
    {
        var handler = new CreateSalaryComponentCommandHandler(_companies, _accounts, _hr);

        var result = await handler.HandleAsync(
            new CreateSalaryComponentCommand(
                _companyId, "Basic Salary", SalaryComponentType.Earning, _groupAccountId));

        Assert.False(result.IsSuccess);
        Assert.Equal(HrPayrollErrorCodes.InvalidGlAccount, result.Error!.Code);
        Assert.Null(_hr.AddedComponent);
    }

    [Fact]
    public async Task CreateComponent_UnknownAccount_FailsWithInvalidGlAccountAndWritesNothing()
    {
        var handler = new CreateSalaryComponentCommandHandler(_companies, _accounts, _hr);

        var result = await handler.HandleAsync(
            new CreateSalaryComponentCommand(
                _companyId, "Basic Salary", SalaryComponentType.Earning, Guid.NewGuid()));

        Assert.False(result.IsSuccess);
        Assert.Equal(HrPayrollErrorCodes.InvalidGlAccount, result.Error!.Code);
        Assert.Null(_hr.AddedComponent);
    }

    // Structure line math + component guards

    [Fact]
    public async Task CreateStructure_FixedPlusPercentageLines_PersistsInOneTransaction()
    {
        var basic = SeedComponent("Basic Salary", SalaryComponentType.Earning);
        var tax = SeedComponent("Income Tax", SalaryComponentType.Deduction);
        var handler = new CreateSalaryStructureCommandHandler(_companies, _hr);

        var result = await handler.HandleAsync(
            new CreateSalaryStructureCommand(
                _companyId,
                "Standard",
                [
                    new SalaryStructureLineInput(basic.Id, 4000m),
                    new SalaryStructureLineInput(tax.Id, 0m, 15m),
                ]));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, _hr.TransactionCount); // header + lines commit atomically
        var saved = _hr.AddedStructure;
        Assert.NotNull(saved);
        Assert.Equal(2, saved!.Lines.Count);
        Assert.Equal(4000m, saved.Lines.Single(l => l.ComponentId == basic.Id).Amount);
        Assert.Equal(15m, saved.Lines.Single(l => l.ComponentId == tax.Id).PercentageOfBase);
        Assert.Equal(2, result.Value!.Lines.Count);
        Assert.Equal("Basic Salary", result.Value.Lines.Single(l => l.ComponentId == basic.Id).ComponentName);
    }

    [Fact]
    public async Task CreateStructure_NoLines_FailsWithNoStructureLinesAndWritesNothing()
    {
        var handler = new CreateSalaryStructureCommandHandler(_companies, _hr);

        var result = await handler.HandleAsync(
            new CreateSalaryStructureCommand(_companyId, "Empty", []));

        Assert.False(result.IsSuccess);
        Assert.Equal(HrPayrollErrorCodes.NoStructureLines, result.Error!.Code);
        Assert.Null(_hr.AddedStructure);
        Assert.Equal(0, _hr.TransactionCount);
    }

    [Fact]
    public async Task CreateStructure_NegativeAmount_FailsWithInvalidLineAmountAndWritesNothing()
    {
        var basic = SeedComponent();
        var handler = new CreateSalaryStructureCommandHandler(_companies, _hr);

        var result = await handler.HandleAsync(
            new CreateSalaryStructureCommand(
                _companyId, "Standard", [new SalaryStructureLineInput(basic.Id, -1m)]));

        Assert.False(result.IsSuccess);
        Assert.Equal(HrPayrollErrorCodes.InvalidLineAmount, result.Error!.Code);
        Assert.Null(_hr.AddedStructure);
    }

    [Fact]
    public async Task CreateStructure_NegativePercentage_FailsWithInvalidLinePercentageAndWritesNothing()
    {
        var basic = SeedComponent();
        var handler = new CreateSalaryStructureCommandHandler(_companies, _hr);

        var result = await handler.HandleAsync(
            new CreateSalaryStructureCommand(
                _companyId, "Standard", [new SalaryStructureLineInput(basic.Id, 0m, -5m)]));

        Assert.False(result.IsSuccess);
        Assert.Equal(HrPayrollErrorCodes.InvalidLinePercentage, result.Error!.Code);
        Assert.Null(_hr.AddedStructure);
    }

    [Fact]
    public async Task CreateStructure_UnknownComponent_FailsWithComponentNotFoundAndWritesNothing()
    {
        var handler = new CreateSalaryStructureCommandHandler(_companies, _hr);

        var result = await handler.HandleAsync(
            new CreateSalaryStructureCommand(
                _companyId, "Standard", [new SalaryStructureLineInput(Guid.NewGuid(), 1000m)]));

        Assert.False(result.IsSuccess);
        Assert.Equal(HrPayrollErrorCodes.ComponentNotFound, result.Error!.Code);
        Assert.Null(_hr.AddedStructure);
    }

    [Fact]
    public async Task CreateStructure_InactiveComponent_FailsWithInactiveComponentAndWritesNothing()
    {
        var dormant = SeedComponent(isActive: false);
        var handler = new CreateSalaryStructureCommandHandler(_companies, _hr);

        var result = await handler.HandleAsync(
            new CreateSalaryStructureCommand(
                _companyId, "Standard", [new SalaryStructureLineInput(dormant.Id, 1000m)]));

        Assert.False(result.IsSuccess);
        Assert.Equal(HrPayrollErrorCodes.InactiveComponent, result.Error!.Code);
        Assert.Null(_hr.AddedStructure);
    }

    [Fact]
    public async Task CreateStructure_ForeignComponent_FailsWithComponentNotFoundAndWritesNothing()
    {
        var foreign = SeedComponent(companyId: Guid.NewGuid());
        var handler = new CreateSalaryStructureCommandHandler(_companies, _hr);

        var result = await handler.HandleAsync(
            new CreateSalaryStructureCommand(
                _companyId, "Standard", [new SalaryStructureLineInput(foreign.Id, 1000m)]));

        Assert.False(result.IsSuccess);
        Assert.Equal(HrPayrollErrorCodes.ComponentNotFound, result.Error!.Code);
        Assert.Null(_hr.AddedStructure);
    }

    // Assignment overlap

    [Fact]
    public async Task Assign_FirstWindow_PersistsAndReturnsDto()
    {
        var employee = SeedEmployee();
        var structure = SeedStructure();
        var handler = new AssignSalaryStructureCommandHandler(_hr);

        var result = await handler.HandleAsync(
            new AssignSalaryStructureCommand(
                _companyId, employee.Id, structure.Id, new DateOnly(2026, 1, 1)));

        Assert.True(result.IsSuccess);
        Assert.Equal(employee.Id, result.Value!.EmployeeId);
        Assert.Equal(structure.Id, result.Value.StructureId);
        Assert.NotNull(_hr.AddedAssignment);
    }

    [Fact]
    public async Task Assign_OverlappingWindow_FailsWithOverlappingAssignmentAndWritesNothing()
    {
        var employee = SeedEmployee();
        var structure = SeedStructure();
        _hr.Seed(new SalaryStructureAssignment
        {
            Id = Guid.NewGuid(),
            CompanyId = _companyId,
            EmployeeId = employee.Id,
            StructureId = structure.Id,
            EffectiveFrom = new DateOnly(2026, 1, 1),
            EffectiveTo = null, // open-ended
            IsActive = true,
        });
        var handler = new AssignSalaryStructureCommandHandler(_hr);

        var result = await handler.HandleAsync(
            new AssignSalaryStructureCommand(
                _companyId, employee.Id, structure.Id, new DateOnly(2026, 6, 1)));

        Assert.False(result.IsSuccess);
        Assert.Equal(HrPayrollErrorCodes.OverlappingAssignment, result.Error!.Code);
        Assert.Null(_hr.AddedAssignment);
    }

    [Fact]
    public async Task Assign_AdjacentNonOverlappingWindow_Succeeds()
    {
        var employee = SeedEmployee();
        var structure = SeedStructure();
        _hr.Seed(new SalaryStructureAssignment
        {
            Id = Guid.NewGuid(),
            CompanyId = _companyId,
            EmployeeId = employee.Id,
            StructureId = structure.Id,
            EffectiveFrom = new DateOnly(2026, 1, 1),
            EffectiveTo = new DateOnly(2026, 5, 31),
            IsActive = true,
        });
        var handler = new AssignSalaryStructureCommandHandler(_hr);

        var result = await handler.HandleAsync(
            new AssignSalaryStructureCommand(
                _companyId, employee.Id, structure.Id, new DateOnly(2026, 6, 1)));

        Assert.True(result.IsSuccess);
        Assert.NotNull(_hr.AddedAssignment);
    }

    [Fact]
    public async Task Assign_InvertedDates_FailsWithInvalidEffectiveDatesAndWritesNothing()
    {
        var employee = SeedEmployee();
        var structure = SeedStructure();
        var handler = new AssignSalaryStructureCommandHandler(_hr);

        var result = await handler.HandleAsync(
            new AssignSalaryStructureCommand(
                _companyId, employee.Id, structure.Id,
                new DateOnly(2026, 6, 1), new DateOnly(2026, 5, 31)));

        Assert.False(result.IsSuccess);
        Assert.Equal(HrPayrollErrorCodes.InvalidEffectiveDates, result.Error!.Code);
        Assert.Null(_hr.AddedAssignment);
    }

    [Fact]
    public async Task Assign_InactiveEmployee_FailsWithInactiveEmployeeAndWritesNothing()
    {
        var employee = SeedEmployee(EmploymentStatus.Left, isActive: false);
        var structure = SeedStructure();
        var handler = new AssignSalaryStructureCommandHandler(_hr);

        var result = await handler.HandleAsync(
            new AssignSalaryStructureCommand(
                _companyId, employee.Id, structure.Id, new DateOnly(2026, 1, 1)));

        Assert.False(result.IsSuccess);
        Assert.Equal(HrPayrollErrorCodes.InactiveEmployee, result.Error!.Code);
        Assert.Null(_hr.AddedAssignment);
    }

    [Fact]
    public async Task Assign_UnknownEmployee_FailsWithEmployeeNotFoundAndWritesNothing()
    {
        var structure = SeedStructure();
        var handler = new AssignSalaryStructureCommandHandler(_hr);

        var result = await handler.HandleAsync(
            new AssignSalaryStructureCommand(
                _companyId, Guid.NewGuid(), structure.Id, new DateOnly(2026, 1, 1)));

        Assert.False(result.IsSuccess);
        Assert.Equal(HrPayrollErrorCodes.EmployeeNotFound, result.Error!.Code);
        Assert.Null(_hr.AddedAssignment);
    }

    [Fact]
    public async Task Assign_InactiveStructure_FailsWithInactiveStructureAndWritesNothing()
    {
        var employee = SeedEmployee();
        var structure = SeedStructure(isActive: false);
        var handler = new AssignSalaryStructureCommandHandler(_hr);

        var result = await handler.HandleAsync(
            new AssignSalaryStructureCommand(
                _companyId, employee.Id, structure.Id, new DateOnly(2026, 1, 1)));

        Assert.False(result.IsSuccess);
        Assert.Equal(HrPayrollErrorCodes.InactiveStructure, result.Error!.Code);
        Assert.Null(_hr.AddedAssignment);
    }
}
