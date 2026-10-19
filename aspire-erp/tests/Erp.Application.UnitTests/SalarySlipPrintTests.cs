using Erp.Application.Features.HrPayroll.Queries;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Printable pay-stub read (spec: receipt view) through the query handler against in-memory
/// doubles: earnings/deductions are split and name-ordered, the employee + company fiscal header
/// are joined in, and a foreign/unknown run or slip resolves to null (404 at the API) instead of
/// leaking another company's data.
/// </summary>
public sealed class SalarySlipPrintTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _employeeId = Guid.NewGuid();
    private readonly FakeCompanyRepository _companies = new();
    private readonly FakeHrPayrollRepository _hr = new();

    private readonly PayrollEntry _entry;
    private readonly SalarySlip _slip;

    public SalarySlipPrintTests()
    {
        _companies.Company = new Company
        {
            Id = _companyId,
            TenantId = Guid.NewGuid(),
            Name = "Acme Industrial",
            TaxId = "ACME-0001",
            CountryCode = "MX",
        };

        _hr.Seed(new Employee
        {
            Id = _employeeId,
            CompanyId = _companyId,
            EmployeeNumber = "EMP-0001",
            FirstName = "Ana",
            LastName = "Reyes",
            WorkEmail = "ana.reyes@example.com",
            DateOfJoining = new DateOnly(2025, 3, 1),
            Status = EmploymentStatus.Active,
            IsActive = true,
            RowVersion = new byte[] { 1 },
            CreatedAt = DateTimeOffset.UtcNow,
        });

        _entry = new PayrollEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = _companyId,
            PayrollNumber = "PE-2025-00001",
            StartDate = new DateOnly(2025, 3, 1),
            EndDate = new DateOnly(2025, 3, 31),
            PostingDate = new DateOnly(2025, 3, 31),
            Status = PayrollEntryStatus.Submitted,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _hr.SeedEntry(_entry);

        _slip = new SalarySlip
        {
            Id = Guid.NewGuid(),
            PayrollEntryId = _entry.Id,
            EmployeeId = _employeeId,
            SlipNumber = "SLIP-2025-00001",
            PaymentDays = 30,
            AbsentDays = 0,
            GrossPay = 5200m,
            TotalDeductions = 400m,
            NetPay = 4800m,
            Status = SalarySlipStatus.Submitted,
        };
        _hr.SeedSlip(_slip);

        // The real repository Includes(s => s.Lines); the in-memory fake returns the instance, so
        // the lines must live on the aggregate navigation as well as the flat seeded list.
        var lines = new[]
        {
            Line("Tax", SalaryComponentType.Deduction, 300m),
            Line("Basic Salary", SalaryComponentType.Earning, 5000m),
            Line("Pension", SalaryComponentType.Deduction, 100m),
            Line("Housing Allowance", SalaryComponentType.Earning, 200m),
        };
        foreach (var line in lines)
        {
            _slip.Lines.Add(line);
        }

        _hr.SeedSlipLines(lines);
    }

    private GetSalarySlipPrintQueryHandler Handler() => new(_hr, _companies);

    private SalarySlipLine Line(string name, SalaryComponentType type, decimal amount) =>
        new()
        {
            Id = Guid.NewGuid(),
            SlipId = _slip.Id,
            ComponentId = Guid.NewGuid(),
            ComponentName = name,
            ComponentType = type,
            Amount = amount,
        };

    [Fact]
    public async Task Builds_the_printable_stub_with_split_name_ordered_lines()
    {
        var result = await Handler().HandleAsync(
            new GetSalarySlipPrintQuery(_companyId, _entry.Id, _slip.Id));

        Assert.NotNull(result);
        var dto = result!;

        Assert.Equal(_slip.Id, dto.SlipId);
        Assert.Equal("SLIP-2025-00001", dto.SlipNumber);
        Assert.Equal(new DateOnly(2025, 3, 31), dto.PostingDate);
        Assert.Equal(5200m, dto.GrossPay);
        Assert.Equal(400m, dto.TotalDeductions);
        Assert.Equal(4800m, dto.NetPay);

        Assert.Equal(["Basic Salary", "Housing Allowance"], dto.Earnings.Select(l => l.ComponentName));
        Assert.Equal(["Pension", "Tax"], dto.Deductions.Select(l => l.ComponentName));
        Assert.All(dto.Earnings, l => Assert.Equal(SalaryComponentType.Earning, l.ComponentType));
        Assert.All(dto.Deductions, l => Assert.Equal(SalaryComponentType.Deduction, l.ComponentType));
    }

    [Fact]
    public async Task Carries_the_employee_and_company_fiscal_header()
    {
        var result = await Handler().HandleAsync(
            new GetSalarySlipPrintQuery(_companyId, _entry.Id, _slip.Id));

        var dto = result!;
        Assert.Equal("Acme Industrial", dto.Company.Name);
        Assert.Equal("ACME-0001", dto.Company.TaxId);
        Assert.Equal("MX", dto.Company.CountryCode);
        Assert.Equal("EMP-0001", dto.Employee.EmployeeNumber);
        Assert.Equal("Ana Reyes", dto.Employee.FullName);
        Assert.Equal("ana.reyes@example.com", dto.Employee.WorkEmail);
    }

    [Fact]
    public async Task Returns_null_for_an_unknown_slip()
    {
        var result = await Handler().HandleAsync(
            new GetSalarySlipPrintQuery(_companyId, _entry.Id, Guid.NewGuid()));

        Assert.Null(result);
    }

    [Fact]
    public async Task Returns_null_for_an_unknown_run()
    {
        var result = await Handler().HandleAsync(
            new GetSalarySlipPrintQuery(_companyId, Guid.NewGuid(), _slip.Id));

        Assert.Null(result);
    }

    [Fact]
    public async Task Returns_null_when_the_run_belongs_to_another_company()
    {
        var result = await Handler().HandleAsync(
            new GetSalarySlipPrintQuery(Guid.NewGuid(), _entry.Id, _slip.Id));

        Assert.Null(result);
    }
}
