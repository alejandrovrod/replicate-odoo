using Erp.Application.DTOs;
using Erp.Application.Features.HrPayroll.Commands;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Tasks 12.3-12.4: the payroll batch engine exercised through the CQRS handlers against
/// in-memory repository doubles - HR-01 identity + clamp per slip, HR-03 eligibility filtering
/// with the excluded-and-reported list, assignment-window edges, payment-day proration,
/// percentage-of-base lines, the HR-02 exact 4-line accrual literals, the disbursement pair
/// with payable-net-zero, the cancel mirror, frozen-date and RowVersion rejections (all with
/// zero-write proofs) and the HR-06 duplicate-slip guard.
/// </summary>
public sealed class PayrollBatchTests
{
    private static readonly DateOnly PeriodStart = new(2026, 10, 1);
    private static readonly DateOnly PeriodEnd = new(2026, 10, 31);
    private static readonly DateOnly PostingDate = new(2026, 10, 31);

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _bankId = Guid.NewGuid();

    private readonly FakeCompanyRepository _companies = new();
    private readonly FakeAccountRepository _accounts = new();
    private readonly FakeBankRepository _banks = new();
    private readonly FakeHrPayrollRepository _hr = new();

    private readonly Account _acc5110;
    private readonly Account _acc2220;
    private readonly Account _acc2225;
    private readonly Account _acc2150;
    private readonly Account _acc1110;

    private readonly Guid _basicId;
    private readonly Guid _housingId;
    private readonly Guid _taxId;
    private readonly Guid _pensionId;
    private readonly Guid _structureId;

    public PayrollBatchTests()
    {
        _companies.Company = new Company
        {
            Id = _companyId,
            TenantId = Guid.NewGuid(),
            Name = "Acme Industrial",
            PayrollPayableAccountCode = "2150",
        };

        _acc5110 = Leaf("5110", "Salary and Wages Expense");
        _acc2220 = Leaf("2220", "Income Tax Payable");
        _acc2225 = Leaf("2225", "Social Security Payable");
        _acc2150 = Leaf("2150", "Payroll Payable");
        _acc1110 = Leaf("1110", "Operating Bank Account");
        _accounts.Seed(_acc5110, _acc2220, _acc2225, _acc2150, _acc1110);

        _accounts.AccountsByCodeMap["5110"] = new[] { _acc5110 };
        _accounts.AccountsByCodeMap["2150"] = new[] { _acc2150 };

        _banks.SeedAccount(new BankAccount
        {
            Id = _bankId,
            TenantId = Guid.NewGuid(),
            CompanyId = _companyId,
            AccountName = "Operating",
            BankName = "Chase",
            AccountNumber = "001",
            GLAccountId = _acc1110.Id,
            IsActive = true,
        });

        var basic = SeedComponent("Basic Salary", SalaryComponentType.Earning, _acc5110.Id);
        var housing = SeedComponent("Housing Allowance", SalaryComponentType.Earning, _acc5110.Id);
        var tax = SeedComponent("Income Tax", SalaryComponentType.Deduction, _acc2220.Id);
        var pension = SeedComponent("Pension", SalaryComponentType.Deduction, _acc2225.Id);

        _basicId = basic.Id;
        _housingId = housing.Id;
        _taxId = tax.Id;
        _pensionId = pension.Id;

        // The spec's standard structure: Basic 4000 + Housing 1000 (earnings -> 5110),
        // Income Tax 600 (-> 2220), Pension 400 (-> 2225). Net per slip: 4000.
        _structureId = SeedStructure("Standard", new List<SalaryStructureLine>
        {
            Line(basic.Id, 4000m),
            Line(housing.Id, 1000m),
            Line(tax.Id, 600m),
            Line(pension.Id, 400m),
        });
    }

    private Account Leaf(string code, string name) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CompanyId = _companyId,
            AccountCode = code,
            AccountName = name,
            RootType = AccountRootType.Asset,
            IsGroup = false,
            IsActive = true,

        };

    private SalaryComponent SeedComponent(
        string name,
        SalaryComponentType type,
        Guid glAccount,
        bool dependsOnPaymentDays = false)
    {
        var component = new SalaryComponent
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CompanyId = _companyId,
            ComponentName = name,
            ComponentType = type,
            DependsOnPaymentDays = dependsOnPaymentDays,
            DefaultGLAccountId = glAccount,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _hr.Seed(component);
        return component;
    }

    private Guid SeedStructure(string name, List<SalaryStructureLine> lines, bool isActive = true)
    {
        var structure = new SalaryStructure
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CompanyId = _companyId,
            StructureName = name,
            IsActive = isActive,
            CreatedAt = DateTimeOffset.UtcNow,
            Lines = lines,
        };

        _hr.Seed(structure);
        return structure.Id;
    }

    private static SalaryStructureLine Line(Guid componentId, decimal amount, decimal? pct = null) =>
        new() { Id = Guid.NewGuid(), ComponentId = componentId, Amount = amount, PercentageOfBase = pct };

    private Employee SeedEmployee(
        string number,
        EmploymentStatus status = EmploymentStatus.Active,
        DateOnly? joined = null,
        DateOnly? relieved = null,
        Guid? structureId = null,
        DateOnly? effectiveFrom = null,
        DateOnly? effectiveTo = null)
    {
        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CompanyId = _companyId,
            EmployeeNumber = number,
            FirstName = "Test",
            LastName = number,
            WorkEmail = $"{number}@acme.test",
            DateOfJoining = joined ?? new DateOnly(2026, 1, 5),
            DateOfRelieving = relieved,
            Status = status,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _hr.Seed(employee);
        _hr.Seed(new SalaryStructureAssignment
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CompanyId = _companyId,
            EmployeeId = employee.Id,
            StructureId = structureId ?? _structureId,
            EffectiveFrom = effectiveFrom ?? new DateOnly(2026, 1, 1),
            EffectiveTo = effectiveTo,
            IsActive = true,
        });

        return employee;
    }

    private SubmitPayrollRunCommandHandler SubmitHandler() => new(_companies, _accounts, _hr);

    private DisbursePayrollCommandHandler DisburseHandler() => new(_companies, _accounts, _banks, _hr);

    private CancelPayrollCommandHandler CancelHandler() => new(_companies, _hr);

    private Task<Erp.Application.Common.Result<PayrollSubmitResultDto>> SubmitAsync(
        IReadOnlyList<PaymentDayOverride>? overrides = null) =>
        SubmitHandler().HandleAsync(new SubmitPayrollRunCommand(
            _companyId, PeriodStart, PeriodEnd, PostingDate, overrides));

    private static decimal AccountTotal(IEnumerable<GLEntry> lines, Guid accountId, Func<GLEntry, decimal> side) =>
        lines.Where(l => l.AccountId == accountId).Sum(side);

    [Fact]
    public async Task Submit_Prices_Hr01_Identity_Per_Slip()
    {
        SeedEmployee("E001");

        var result = await SubmitAsync();

        Assert.True(result.IsSuccess);
        var run = result.Value!;
        Assert.Equal(PayrollEntryStatus.Submitted, run.Entry.Status);
        Assert.Equal("PE-2026-00001", run.Entry.PayrollNumber);
        Assert.Equal(1, run.CreatedSlipCount);
        Assert.Empty(run.Skipped);
        Assert.Equal(5000m, run.Entry.TotalGrossPay);
        Assert.Equal(1000m, run.Entry.TotalDeductions);
        Assert.Equal(4000m, run.Entry.TotalNetPay);

        var slip = Assert.Single(_hr.Slips);
        Assert.Equal(5000m, slip.GrossPay);
        Assert.Equal(1000m, slip.TotalDeductions);
        Assert.Equal(4000m, slip.NetPay);
        Assert.Equal(SalarySlipStatus.Submitted, slip.Status);
        Assert.Equal(4, _hr.SlipLines.Count(l => l.SlipId == slip.Id));
    }

    [Fact]
    public async Task Submit_Clamps_Net_At_Zero_And_Absorbs_Surplus_In_Voucher()
    {
        // Deductions (6400) exceed gross (5000): the slip floors net at zero (HR-01) and the
        // voucher credits only the effective 5000 pro-rata, so it still balances.
        var big = SeedComponent("Mega Tax", SalaryComponentType.Deduction, _acc2220.Id);
        var heavyId = SeedStructure("Heavy Tax", new List<SalaryStructureLine>
        {
            Line(_basicId, 4000m),
            Line(_housingId, 1000m),
            Line(big.Id, 6000m),
            Line(_pensionId, 400m),
        });

        SeedEmployee("E001", structureId: heavyId);

        var result = await SubmitAsync();

        Assert.True(result.IsSuccess);
        var slip = Assert.Single(_hr.Slips);
        Assert.Equal(5000m, slip.GrossPay);
        Assert.Equal(6400m, slip.TotalDeductions);
        Assert.Equal(0m, slip.NetPay);

        var gl = _hr.AddedGlEntries;
        Assert.Equal(gl.Sum(l => l.Debit), gl.Sum(l => l.Credit));
        Assert.Equal(5000m, AccountTotal(gl, _acc5110.Id, l => l.Debit));
        Assert.Equal(5000m, AccountTotal(gl, _acc2220.Id, l => l.Credit)
            + AccountTotal(gl, _acc2225.Id, l => l.Credit));
        Assert.Equal(0m, AccountTotal(gl, _acc2150.Id, l => l.Credit));
    }

    [Fact]
    public async Task Submit_Filters_Ineligible_And_Reports_Excluded()
    {
        var eligible = SeedEmployee("E001");
        SeedEmployee("E002", status: EmploymentStatus.Inactive);
        SeedEmployee("E003", joined: new DateOnly(2026, 11, 1));
        SeedEmployee("E004", relieved: new DateOnly(2026, 9, 30));

        var result = await SubmitAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.CreatedSlipCount);
        Assert.Equal(eligible.Id, Assert.Single(_hr.Slips).EmployeeId);

        Assert.Equal(3, result.Value.Skipped.Count);
        var skippedIds = result.Value.Skipped.Select(s => s.EmployeeId).ToHashSet();
        Assert.DoesNotContain(eligible.Id, skippedIds);
    }

    [Fact]
    public async Task Submit_Respects_Assignment_Window_Edges()
    {
        // Inclusive edges: EffectiveFrom == period end overlaps; EffectiveTo == day before
        // start does not; EffectiveTo == period start overlaps.
        var onEndEdge = SeedEmployee("E001", effectiveFrom: new DateOnly(2026, 10, 31));
        var beforeStart = SeedEmployee(
            "E002", effectiveFrom: new DateOnly(2026, 1, 1), effectiveTo: new DateOnly(2026, 9, 30));
        var onStartEdge = SeedEmployee(
            "E003", effectiveFrom: new DateOnly(2026, 1, 1), effectiveTo: new DateOnly(2026, 10, 1));

        var result = await SubmitAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.CreatedSlipCount);
        var slipEmployees = _hr.Slips.Select(s => s.EmployeeId).ToHashSet();
        Assert.Contains(onEndEdge.Id, slipEmployees);
        Assert.Contains(onStartEdge.Id, slipEmployees);
        Assert.DoesNotContain(beforeStart.Id, slipEmployees);
        Assert.Contains(result.Value.Skipped, s => s.EmployeeId == beforeStart.Id);
    }

    [Fact]
    public async Task Submit_Prorates_PaymentDay_Lines()
    {
        var daily = SeedComponent("Daily Wage", SalaryComponentType.Earning, _acc5110.Id, dependsOnPaymentDays: true);
        var fixedAllowance = SeedComponent("Fixed Allowance", SalaryComponentType.Earning, _acc5110.Id);
        var flatTax = SeedComponent("Flat Tax", SalaryComponentType.Deduction, _acc2220.Id);
        var proratedId = SeedStructure("Prorated", new List<SalaryStructureLine>
        {
            Line(daily.Id, 4000m),
            Line(fixedAllowance.Id, 1000m),
            Line(flatTax.Id, 1000m),
        });

        var employee = SeedEmployee("E001", structureId: proratedId);

        var result = await SubmitAsync(new[]
        {
            new PaymentDayOverride(employee.Id, PaymentDays: 15, AbsentDays: 15),
        });

        Assert.True(result.IsSuccess);
        var slip = Assert.Single(_hr.Slips);
        Assert.Equal(15, slip.PaymentDays);
        Assert.Equal(15, slip.AbsentDays);
        Assert.Equal(3000m, slip.GrossPay);
        Assert.Equal(1000m, slip.TotalDeductions);
        Assert.Equal(2000m, slip.NetPay);
    }

    [Fact]
    public async Task Submit_Prices_Percentage_Of_Base_Lines()
    {
        var bonus = SeedComponent("Performance Bonus", SalaryComponentType.Earning, _acc5110.Id);
        var pctId = SeedStructure("With Bonus", new List<SalaryStructureLine>
        {
            Line(_basicId, 4000m),
            Line(bonus.Id, 0m, pct: 10m),
        });

        SeedEmployee("E001", structureId: pctId);

        var result = await SubmitAsync();

        Assert.True(result.IsSuccess);
        var slip = Assert.Single(_hr.Slips);
        Assert.Equal(4400m, slip.GrossPay);
        Assert.Equal(0m, slip.TotalDeductions);
        Assert.Equal(4400m, slip.NetPay);
        Assert.Contains(_hr.SlipLines.Where(l => l.SlipId == slip.Id),
            l => l.ComponentId == bonus.Id && l.Amount == 400m);
    }

    [Fact]
    public async Task Submit_Posts_Hr02_Exact_Four_Line_Accrual()
    {
        // Spec HR-02 verbatim: 20 employees x (5000 gross / 600 tax / 400 pension / 4000 net).
        for (var i = 1; i <= 20; i++)
        {
            SeedEmployee($"E{i:D3}");
        }

        var result = await SubmitAsync();

        Assert.True(result.IsSuccess);
        var run = result.Value!;
        Assert.Equal(100000m, run.Entry.TotalGrossPay);
        Assert.Equal(20000m, run.Entry.TotalDeductions);
        Assert.Equal(80000m, run.Entry.TotalNetPay);
        Assert.StartsWith("PYR-2026-", run.Entry.AccrualVoucherNo);

        var gl = _hr.AddedGlEntries;
        Assert.Equal(4, gl.Count);
        Assert.Equal(100000m, AccountTotal(gl, _acc5110.Id, l => l.Debit));
        Assert.Equal(12000m, AccountTotal(gl, _acc2220.Id, l => l.Credit));
        Assert.Equal(8000m, AccountTotal(gl, _acc2225.Id, l => l.Credit));
        Assert.Equal(80000m, AccountTotal(gl, _acc2150.Id, l => l.Credit));
        Assert.Equal(gl.Sum(l => l.Debit), gl.Sum(l => l.Credit));
        Assert.All(gl, l => Assert.Equal(run.Entry.AccrualVoucherNo, l.VoucherNo));
    }

    [Fact]
    public async Task Submit_Rejects_Inverted_Period_With_Zero_Writes()
    {
        SeedEmployee("E001");

        var result = await SubmitHandler().HandleAsync(new SubmitPayrollRunCommand(
            _companyId, PeriodEnd, PeriodStart, PostingDate));

        Assert.False(result.IsSuccess);
        Assert.Equal(HrPayrollErrorCodes.InvalidPayrollPeriod, result.Error!.Code);
        Assert.Empty(_hr.Entries);
        Assert.Empty(_hr.Slips);
        Assert.Empty(_hr.AddedGlEntries);
    }

    [Fact]
    public async Task Submit_Rejects_Empty_Run_With_Zero_Writes()
    {
        var result = await SubmitAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal(HrPayrollErrorCodes.NoEligibleEmployees, result.Error!.Code);
        Assert.Empty(_hr.Entries);
        Assert.Empty(_hr.AddedGlEntries);
    }

    [Fact]
    public async Task Submit_Rejects_Frozen_Posting_Date_With_Zero_Writes()
    {
        SeedEmployee("E001");
        _companies.Company!.FrozenAccountsDate = PostingDate;

        var result = await SubmitAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal(AccountingErrorCodes.FiscalPeriodLocked, result.Error!.Code);
        Assert.Empty(_hr.Entries);
        Assert.Empty(_hr.Slips);
        Assert.Empty(_hr.AddedGlEntries);
    }

    [Fact]
    public async Task Submit_RowVersion_Race_Aborts_With_Zero_Writes()
    {
        SeedEmployee("E001");
        _hr.FailNextEntryUpdate = true;

        var result = await SubmitAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal(ConcurrencyErrorCodes.ConcurrencyConflict, result.Error!.Code);
        Assert.Empty(_hr.Entries);
        Assert.Empty(_hr.Slips);
        Assert.Empty(_hr.AddedGlEntries);
    }

    [Fact]
    public async Task Disburse_Posts_Exact_Pair_And_Marks_Paid()
    {
        SeedEmployee("E001");
        var submitted = (await SubmitAsync()).Value!.Entry;

        var result = await DisburseHandler().HandleAsync(new DisbursePayrollCommand(
            _companyId, submitted.Id, _bankId, PostingDate));

        Assert.True(result.IsSuccess);
        Assert.Equal(PayrollEntryStatus.Paid, result.Value!.Status);
        Assert.StartsWith("PYR-2026-", result.Value.PaymentVoucherNo);

        // The run's own lines: accrual (4) + disbursement (2).
        Assert.Equal(6, _hr.AddedGlEntries.Count);
        var pair = _hr.AddedGlEntries
            .Where(l => l.VoucherNo == result.Value.PaymentVoucherNo)
            .ToList();
        Assert.Equal(2, pair.Count);
        Assert.Equal(4000m, AccountTotal(pair, _acc2150.Id, l => l.Debit));
        Assert.Equal(4000m, AccountTotal(pair, _acc1110.Id, l => l.Credit));

        // Payable-net-zero: accrual credited 2150 with 4000, disbursement debits it back.
        Assert.Equal(0m, AccountTotal(_hr.AddedGlEntries, _acc2150.Id, l => l.Debit)
            - AccountTotal(_hr.AddedGlEntries, _acc2150.Id, l => l.Credit));
    }

    [Fact]
    public async Task Disburse_Rejects_Non_Submitted_And_Unknown_Entries()
    {
        var draft = new PayrollEntry
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CompanyId = _companyId,
            PayrollNumber = "PE-2026-00001",
            StartDate = PeriodStart,
            EndDate = PeriodEnd,
            PostingDate = PostingDate,
            Status = PayrollEntryStatus.Draft,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _hr.SeedEntry(draft);

        var draftResult = await DisburseHandler().HandleAsync(new DisbursePayrollCommand(
            _companyId, draft.Id, _bankId, PostingDate));
        Assert.False(draftResult.IsSuccess);
        Assert.Equal(HrPayrollErrorCodes.InvalidStatusTransition, draftResult.Error!.Code);

        var missingResult = await DisburseHandler().HandleAsync(new DisbursePayrollCommand(
            _companyId, Guid.NewGuid(), _bankId, PostingDate));
        Assert.False(missingResult.IsSuccess);
        Assert.Equal(HrPayrollErrorCodes.PayrollEntryNotFound, missingResult.Error!.Code);

        Assert.Empty(_hr.AddedGlEntries);
    }

    [Fact]
    public async Task Disburse_Rejects_Frozen_Date_And_Stale_RowVersion_With_Zero_Writes()
    {
        SeedEmployee("E001");
        var submitted = (await SubmitAsync()).Value!.Entry;
        var glCount = _hr.AddedGlEntries.Count;

        _companies.Company!.FrozenAccountsDate = PostingDate;
        var frozen = await DisburseHandler().HandleAsync(new DisbursePayrollCommand(
            _companyId, submitted.Id, _bankId, PostingDate));
        Assert.False(frozen.IsSuccess);
        Assert.Equal(AccountingErrorCodes.FiscalPeriodLocked, frozen.Error!.Code);
        _companies.Company.FrozenAccountsDate = null;

        var stale = await DisburseHandler().HandleAsync(new DisbursePayrollCommand(
            _companyId, submitted.Id, _bankId, PostingDate, RowVersion: new byte[] { 9, 9, 9 }));
        Assert.False(stale.IsSuccess);
        Assert.Equal(ConcurrencyErrorCodes.ConcurrencyConflict, stale.Error!.Code);

        Assert.Equal(PayrollEntryStatus.Submitted, _hr.Entries.Single().Status);
        Assert.Equal(glCount, _hr.AddedGlEntries.Count);
    }

    [Fact]
    public async Task Cancel_Mirrors_Accrual_And_Cancels_Slips()
    {
        SeedEmployee("E001");
        var submitted = (await SubmitAsync()).Value!.Entry;
        var accrual = _hr.AddedGlEntries.ToList();

        var result = await CancelHandler().HandleAsync(
            new CancelPayrollCommand(_companyId, submitted.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal(PayrollEntryStatus.Cancelled, result.Value!.Status);

        var mirror = _hr.AddedGlEntries.Skip(accrual.Count).ToList();
        Assert.Equal(accrual.Count, mirror.Count);
        foreach (var original in accrual)
        {
            var reversal = Assert.Single(mirror, m => m.AccountId == original.AccountId);
            Assert.Equal(original.Credit, reversal.Debit);
            Assert.Equal(original.Debit, reversal.Credit);
            Assert.True(reversal.IsCancelled);
            Assert.Equal(submitted.Id, reversal.VoucherId);
        }

        Assert.All(_hr.Slips, s => Assert.Equal(SalarySlipStatus.Cancelled, s.Status));

        // Payable-net-zero after cancel: accrual Cr 4000, mirror Dr 4000.
        Assert.Equal(0m, AccountTotal(_hr.AddedGlEntries, _acc2150.Id, l => l.Debit)
            - AccountTotal(_hr.AddedGlEntries, _acc2150.Id, l => l.Credit));
    }

    [Fact]
    public async Task Cancel_Rejects_Double_Cancel_And_Paid_Run_With_Zero_New_Writes()
    {
        SeedEmployee("E001");
        var submitted = (await SubmitAsync()).Value!.Entry;

        var first = await CancelHandler().HandleAsync(
            new CancelPayrollCommand(_companyId, submitted.Id));
        Assert.True(first.IsSuccess);
        var glCount = _hr.AddedGlEntries.Count;

        var second = await CancelHandler().HandleAsync(
            new CancelPayrollCommand(_companyId, submitted.Id));
        Assert.False(second.IsSuccess);
        Assert.Equal(HrPayrollErrorCodes.InvalidStatusTransition, second.Error!.Code);
        Assert.Equal(glCount, _hr.AddedGlEntries.Count);

        SeedEmployee("E002");
        var paid = (await SubmitAsync()).Value!.Entry;
        var disbursed = await DisburseHandler().HandleAsync(new DisbursePayrollCommand(
            _companyId, paid.Id, _bankId, PostingDate));
        Assert.True(disbursed.IsSuccess);
        glCount = _hr.AddedGlEntries.Count;

        var cancelPaid = await CancelHandler().HandleAsync(
            new CancelPayrollCommand(_companyId, paid.Id));
        Assert.False(cancelPaid.IsSuccess);
        Assert.Equal(HrPayrollErrorCodes.InvalidStatusTransition, cancelPaid.Error!.Code);
        Assert.Equal(glCount, _hr.AddedGlEntries.Count);
        Assert.Equal(PayrollEntryStatus.Paid, _hr.Entries.Single(e => e.Id == paid.Id).Status);
    }

    [Fact]
    public async Task Cancel_Rejects_Frozen_Date_And_Stale_RowVersion_With_Zero_Writes()
    {
        SeedEmployee("E001");
        var submitted = (await SubmitAsync()).Value!.Entry;
        var glCount = _hr.AddedGlEntries.Count;

        _companies.Company!.FrozenAccountsDate = PostingDate;
        var frozen = await CancelHandler().HandleAsync(
            new CancelPayrollCommand(_companyId, submitted.Id, PostingDate));
        Assert.False(frozen.IsSuccess);
        Assert.Equal(AccountingErrorCodes.FiscalPeriodLocked, frozen.Error!.Code);
        _companies.Company.FrozenAccountsDate = null;

        var stale = await CancelHandler().HandleAsync(
            new CancelPayrollCommand(_companyId, submitted.Id, RowVersion: new byte[] { 7 }));
        Assert.False(stale.IsSuccess);
        Assert.Equal(ConcurrencyErrorCodes.ConcurrencyConflict, stale.Error!.Code);

        Assert.Equal(PayrollEntryStatus.Submitted, _hr.Entries.Single().Status);
        Assert.All(_hr.Slips, s => Assert.Equal(SalarySlipStatus.Submitted, s.Status));
        Assert.Equal(glCount, _hr.AddedGlEntries.Count);
    }

    [Fact]
    public async Task Repository_Rejects_Duplicate_Slip_With_Typed_Conflict()
    {
        var entryId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        _hr.SeedSlip(new SalarySlip
        {
            Id = Guid.NewGuid(),
            PayrollEntryId = entryId,
            EmployeeId = employeeId,
            SlipNumber = "PE-2026-00001-001",
            Status = SalarySlipStatus.Submitted,
        });

        var duplicate = new SalarySlip
        {
            Id = Guid.NewGuid(),
            PayrollEntryId = entryId,
            EmployeeId = employeeId,
            SlipNumber = "PE-2026-00001-002",
            Status = SalarySlipStatus.Draft,
        };

        var slipsBefore = _hr.Slips.Count;
        var ex = await Assert.ThrowsAsync<HrValidationException>(
            () => _hr.AddSlipAsync(duplicate));
        Assert.Equal(HrPayrollErrorCodes.DuplicateSalarySlip, ex.Code);
        Assert.Equal(slipsBefore, _hr.Slips.Count);
    }

    [Fact]
    public async Task Submit_Rejects_Overlapping_Period_With_Zero_Writes()
    {
        SeedEmployee("E001");
        var first = await SubmitAsync();
        Assert.True(first.IsSuccess);
        var glCount = _hr.AddedGlEntries.Count;

        // Same October window again: the live Submitted entry blocks the re-run.
        var second = await SubmitAsync();
        Assert.False(second.IsSuccess);
        Assert.Equal(HrPayrollErrorCodes.PayrollPeriodOverlap, second.Error!.Code);

        // Partial-overlap (October-November) is blocked too; the winner is untouched.
        var partial = await SubmitHandler().HandleAsync(new SubmitPayrollRunCommand(
            _companyId, new DateOnly(2026, 10, 15), new DateOnly(2026, 11, 15), new DateOnly(2026, 11, 15)));
        Assert.False(partial.IsSuccess);
        Assert.Equal(HrPayrollErrorCodes.PayrollPeriodOverlap, partial.Error!.Code);

        Assert.Single(_hr.Entries);
        Assert.Equal(glCount, _hr.AddedGlEntries.Count);
    }

    [Fact]
    public async Task Submit_Allows_Rerun_After_Cancel_Releases_Period()
    {
        SeedEmployee("E001");
        var first = await SubmitAsync();
        Assert.True(first.IsSuccess);

        var cancelled = await CancelHandler().HandleAsync(
            new CancelPayrollCommand(_companyId, first.Value!.Entry.Id));
        Assert.True(cancelled.IsSuccess);

        // The Cancelled run released its accrual (mirror) and its period: re-running the
        // same October window prices the employee again instead of 409ing.
        var rerun = await SubmitAsync();
        Assert.True(rerun.IsSuccess);
        Assert.Equal(1, rerun.Value!.CreatedSlipCount);
        Assert.Equal(2, _hr.Entries.Count);
    }
}
