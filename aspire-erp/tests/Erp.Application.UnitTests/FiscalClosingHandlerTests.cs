using Erp.Application.DTOs;
using Erp.Application.Features.FiscalClosing;
using Erp.Application.Features.GeneralLedger.PeriodClosing;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// R-13 Fase 2 (tasks.md Phase 3 acceptance): the Fiscal Closing CQRS pipeline exercised through
/// the handlers against in-memory repository doubles — create/close fiscal year, Draft creation
/// pre-checks, the submit validation order (guards → duplicate → balances → retained → math →
/// persist), same-key replay idempotency, empty-year rejection and the append-only cancel.
/// Live serializable races (FC-10) and the migration DDL need SQL Server (out of reach here);
/// the filtered unique index + serializable txn are that race's backstop.
/// </summary>
public sealed class FiscalClosingHandlerTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly FakeCompanyRepository _companies = new();
    private readonly FakeAccountRepository _accounts = new();
    private readonly FakeFiscalYearRepository _years = new();
    private readonly FakeClosingRepository _closings = new();

    private readonly Account _revenue;
    private readonly Account _expenses;
    private readonly Account _retained;
    private readonly Account _group;

    public FiscalClosingHandlerTests()
    {
        _companies.Company = new Company
        {
            Id = _companyId,
            TenantId = _tenantId,
            Name = "Acme Industrial",
        };

        _revenue = PlLeaf("4000", "Sales Revenue", AccountRootType.Income);
        _expenses = PlLeaf("5000", "Operating Expenses", AccountRootType.Expense);
        _retained = PlLeaf("3100", "Retained Earnings", AccountRootType.Equity);
        _group = new Account
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            CompanyId = _companyId,
            AccountCode = "1000",
            AccountName = "Assets",
            RootType = AccountRootType.Asset,
            IsGroup = true,
            IsActive = true,
        };

        _accounts.Seed(_revenue, _expenses, _retained, _group);
        _companies.Company.DefaultRetainedEarningsAccountId = _retained.Id;
    }

    private Account PlLeaf(string code, string name, AccountRootType root) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = _tenantId,
        CompanyId = _companyId,
        AccountCode = code,
        AccountName = name,
        RootType = root,
        IsGroup = false,
        IsActive = true,
    };

    private FiscalYear Year() =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            CompanyId = _companyId,
            YearName = "FY-2025",
            StartDate = new DateOnly(2025, 1, 1),
            EndDate = new DateOnly(2025, 12, 31),
            IsClosed = false,
            RowVersion = new byte[] { 1, 2, 3 },
        };

    private CreateFiscalYearCommandHandler CreateYearHandler() => new(_companies, _years);
    private CloseFiscalYearCommandHandler CloseYearHandler() => new(_years, _closings);
    private CreatePeriodClosingVoucherCommandHandler CreateHandler() => new(_companies, _years, _accounts, _closings);
    private SubmitPeriodClosingVoucherCommandHandler SubmitHandler() => new(_closings, _years, _companies, _accounts);
    private CancelPeriodClosingVoucherCommandHandler CancelHandler() => new(_closings, _years, _companies);

    // ---- CreateFiscalYear / CloseFiscalYear ----

    [Fact]
    public async Task CreateFiscalYear_OpenYear_CreatesOpen()
    {
        var result = await CreateYearHandler().HandleAsync(
            new CreateFiscalYearCommand(_companyId, "FY-2025", new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31)));

        Assert.True(result.IsSuccess);
        Assert.Equal("FY-2025", result.Value!.YearName);
        Assert.False(result.Value.IsClosed);
        Assert.Single(_years.Years);
    }

    [Fact]
    public async Task CreateFiscalYear_OverlappingWindow_RejectedAndPersistsNothing()
    {
        _years.Seed(Year());
        var before = _years.Years.Count;

        var result = await CreateYearHandler().HandleAsync(
            new CreateFiscalYearCommand(_companyId, "FY-2025b", new DateOnly(2025, 6, 1), new DateOnly(2026, 5, 31)));

        Assert.False(result.IsSuccess);
        Assert.Equal(FiscalClosingErrorCodes.FiscalYearOverlap, result.Error!.Code);
        Assert.Equal(before, _years.Years.Count);
    }

    [Fact]
    public async Task CloseFiscalYear_NoDrafts_Closes()
    {
        var year = Year();
        _years.Seed(year);

        var result = await CloseYearHandler().HandleAsync(
            new CloseFiscalYearCommand(year.Id, _companyId, year.RowVersion));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsClosed);
    }

    [Fact]
    public async Task CloseFiscalYear_WithDraftVoucher_Refused()
    {
        var year = Year();
        _years.Seed(year);
        _closings.SeedDraft(year, _companyId, _tenantId, _retained.Id);

        var result = await CloseYearHandler().HandleAsync(
            new CloseFiscalYearCommand(year.Id, _companyId, year.RowVersion));

        Assert.False(result.IsSuccess);
        Assert.Equal(FiscalClosingErrorCodes.PeriodClosingInvalidTransition, result.Error!.Code);
        Assert.False(year.IsClosed);
    }

    [Fact]
    public async Task CloseFiscalYear_StaleRowVersion_Conflict()
    {
        var year = Year();
        _years.Seed(year);

        var result = await CloseYearHandler().HandleAsync(
            new CloseFiscalYearCommand(year.Id, _companyId, new byte[] { 9, 9, 9 }));

        Assert.False(result.IsSuccess);
        Assert.Equal(FiscalClosingErrorCodes.ConcurrencyConflict, result.Error!.Code);
    }

    // ---- CreatePeriodClosingVoucher ----

    [Fact]
    public async Task CreateVoucher_DateOutsideYear_Rejected()
    {
        var year = Year();
        _years.Seed(year);

        var result = await CreateHandler().HandleAsync(
            new CreatePeriodClosingVoucherCommand(
                _companyId, year.Id, new DateOnly(2026, 1, 5), _retained.Id, null, null));

        Assert.False(result.IsSuccess);
        Assert.Equal(FiscalClosingErrorCodes.ClosingDateOutsideFiscalYear, result.Error!.Code);
        Assert.Empty(_closings.Vouchers);
    }

    [Fact]
    public async Task CreateVoucher_GroupRetainedAccount_Rejected()
    {
        var year = Year();
        _years.Seed(year);

        var result = await CreateHandler().HandleAsync(
            new CreatePeriodClosingVoucherCommand(
                _companyId, year.Id, new DateOnly(2025, 12, 31), _group.Id, null, null));

        Assert.False(result.IsSuccess);
        Assert.Equal(FiscalClosingErrorCodes.InvalidRetainedEarningsAccount, result.Error!.Code);
    }

    [Fact]
    public async Task CreateVoucher_NullRetained_ResolvesCompanyDefault()
    {
        var year = Year();
        _years.Seed(year);

        var result = await CreateHandler().HandleAsync(
            new CreatePeriodClosingVoucherCommand(
                _companyId, year.Id, new DateOnly(2025, 12, 31), Guid.Empty, null, null));

        Assert.True(result.IsSuccess);
        Assert.Equal(_retained.Id, result.Value!.RetainedEarningsAccountId);
        Assert.Equal(DocumentStatus.Draft, result.Value.DocumentStatus);
    }

    [Fact]
    public async Task CreateVoucher_SameIdempotencyKey_ReplaysSameDraft()
    {
        var year = Year();
        _years.Seed(year);

        var first = await CreateHandler().HandleAsync(
            new CreatePeriodClosingVoucherCommand(
                _companyId, year.Id, new DateOnly(2025, 12, 31), _retained.Id, null, "k-1"));
        var replay = await CreateHandler().HandleAsync(
            new CreatePeriodClosingVoucherCommand(
                _companyId, year.Id, new DateOnly(2025, 12, 31), _retained.Id, null, "k-1"));

        Assert.True(replay.IsSuccess);
        Assert.Equal(first.Value!.Id, replay.Value!.Id);
        Assert.Single(_closings.Vouchers);
    }

    // ---- Submit ----

    private void SeedProfitYear(FiscalYear year)
    {
        _years.Seed(year);
        _closings.SeedBalances(
            new UnclosedPLBalance(_revenue.Id, "4000", "Sales Revenue", AccountRootType.Income, 500_000m),
            new UnclosedPLBalance(_expenses.Id, "5000", "Operating Expenses", AccountRootType.Expense, -380_000m));
    }

    [Fact]
    public async Task Submit_ProfitFixture_PostsBalancedClose()
    {
        var year = Year();
        SeedProfitYear(year);
        var draft = _closings.SeedDraft(year, _companyId, _tenantId, _retained.Id);

        var result = await SubmitHandler().HandleAsync(
            new SubmitPeriodClosingVoucherCommand(draft.Id, _companyId, draft.RowVersion, "submit-1"));

        Assert.True(result.IsSuccess);
        Assert.Equal(DocumentStatus.Submitted, result.Value!.DocumentStatus);
        Assert.StartsWith("PCV-2025-", result.Value.VoucherNo);

        var gl = _closings.GLEntries;
        Assert.Equal(3, gl.Count);
        Assert.Equal(500_000m, Assert.Single(gl, e => e.AccountId == _revenue.Id).Debit);
        Assert.Equal(380_000m, Assert.Single(gl, e => e.AccountId == _expenses.Id).Credit);
        var retainedRow = Assert.Single(gl, e => e.AccountId == _retained.Id);
        Assert.Equal(120_000m, retainedRow.Credit);
        Assert.Equal(0m, retainedRow.Debit);
        Assert.True(Math.Abs(gl.Sum(e => e.Debit) - gl.Sum(e => e.Credit)) <= 0.0001m);
        Assert.All(gl, e => Assert.False(e.IsCancelled));
        Assert.Equal(3, _closings.Lines.Count);
    }

    [Fact]
    public async Task Submit_SameKeyReplay_ReturnsRecordedSuccessWithZeroNewRows()
    {
        var year = Year();
        SeedProfitYear(year);
        var draft = _closings.SeedDraft(year, _companyId, _tenantId, _retained.Id);

        var first = await SubmitHandler().HandleAsync(
            new SubmitPeriodClosingVoucherCommand(draft.Id, _companyId, draft.RowVersion, "submit-1"));
        var replay = await SubmitHandler().HandleAsync(
            new SubmitPeriodClosingVoucherCommand(draft.Id, _companyId, null, "submit-1"));

        Assert.True(replay.IsSuccess);
        Assert.Equal(first.Value!.Id, replay.Value!.Id);
        Assert.Equal(3, _closings.GLEntries.Count);
    }

    [Fact]
    public async Task Submit_AgainWithoutKey_InvalidTransition()
    {
        var year = Year();
        SeedProfitYear(year);
        var draft = _closings.SeedDraft(year, _companyId, _tenantId, _retained.Id);
        await SubmitHandler().HandleAsync(
            new SubmitPeriodClosingVoucherCommand(draft.Id, _companyId, draft.RowVersion, "submit-1"));

        var again = await SubmitHandler().HandleAsync(
            new SubmitPeriodClosingVoucherCommand(draft.Id, _companyId));

        Assert.False(again.IsSuccess);
        Assert.Equal(FiscalClosingErrorCodes.PeriodClosingInvalidTransition, again.Error!.Code);
        Assert.Equal(3, _closings.GLEntries.Count);
    }

    [Fact]
    public async Task Submit_EmptyYear_RejectedAndStaysDraft()
    {
        var year = Year();
        _years.Seed(year);
        var draft = _closings.SeedDraft(year, _companyId, _tenantId, _retained.Id);

        var result = await SubmitHandler().HandleAsync(
            new SubmitPeriodClosingVoucherCommand(draft.Id, _companyId));

        Assert.False(result.IsSuccess);
        Assert.Equal(FiscalClosingErrorCodes.NoClosingBalances, result.Error!.Code);
        Assert.Equal(DocumentStatus.Draft, draft.DocumentStatus);
        Assert.Empty(_closings.GLEntries);
    }

    [Fact]
    public async Task Submit_IntoClosedYear_RejectedAndPersistsNothing()
    {
        var year = Year();
        year.Close();
        SeedProfitYear(year);
        var draft = _closings.SeedDraft(year, _companyId, _tenantId, _retained.Id);

        var result = await SubmitHandler().HandleAsync(
            new SubmitPeriodClosingVoucherCommand(draft.Id, _companyId));

        Assert.False(result.IsSuccess);
        Assert.Equal(FiscalClosingErrorCodes.FiscalYearClosed, result.Error!.Code);
        Assert.Empty(_closings.GLEntries);
    }

    [Fact]
    public async Task Submit_SecondVoucherForSameYear_DuplicateRejected()
    {
        var year = Year();
        SeedProfitYear(year);
        var first = _closings.SeedDraft(year, _companyId, _tenantId, _retained.Id);
        await SubmitHandler().HandleAsync(
            new SubmitPeriodClosingVoucherCommand(first.Id, _companyId, first.RowVersion, "s-1"));

        var second = _closings.SeedDraft(year, _companyId, _tenantId, _retained.Id);
        _closings.SeedBalances(
            new UnclosedPLBalance(_revenue.Id, "4000", "Sales Revenue", AccountRootType.Income, 10m));
        var result = await SubmitHandler().HandleAsync(
            new SubmitPeriodClosingVoucherCommand(second.Id, _companyId));

        Assert.False(result.IsSuccess);
        Assert.Equal(FiscalClosingErrorCodes.DuplicateClosingForFiscalYear, result.Error!.Code);
    }

    // ---- Cancel ----

    [Fact]
    public async Task Cancel_Submitted_AppendsMirroredReversal()
    {
        var year = Year();
        SeedProfitYear(year);
        var draft = _closings.SeedDraft(year, _companyId, _tenantId, _retained.Id);
        var submitted = await SubmitHandler().HandleAsync(
            new SubmitPeriodClosingVoucherCommand(draft.Id, _companyId, draft.RowVersion, "s-1"));
        var originals = _closings.GLEntries.ToList();

        var result = await CancelHandler().HandleAsync(
            new CancelPeriodClosingVoucherCommand(draft.Id, _companyId, submitted.Value!.RowVersion));

        Assert.True(result.IsSuccess);
        Assert.Equal(DocumentStatus.Cancelled, result.Value!.DocumentStatus);

        // Originals byte-identical: still IsCancelled=false with the same D/C.
        Assert.Equal(6, _closings.GLEntries.Count);
        foreach (var original in originals)
        {
            var current = _closings.GLEntries.First(e => e.Id == original.Id);
            Assert.False(current.IsCancelled);
            Assert.Equal(original.Debit, current.Debit);
            Assert.Equal(original.Credit, current.Credit);
        }

        var reversals = _closings.GLEntries.Skip(3).ToList();
        Assert.All(reversals, r => Assert.StartsWith("Reversal of ", r.Remarks));
        Assert.True(Math.Abs(
            _closings.GLEntries.Sum(e => e.Debit) - _closings.GLEntries.Sum(e => e.Credit)) <= 0.0001m);

        var doubleCancel = await CancelHandler().HandleAsync(
            new CancelPeriodClosingVoucherCommand(draft.Id, _companyId));
        Assert.False(doubleCancel.IsSuccess);
        Assert.Equal(FiscalClosingErrorCodes.PeriodClosingAlreadyCancelled, doubleCancel.Error!.Code);
    }

    [Fact]
    public async Task Cancel_DraftVoucher_InvalidTransition()
    {
        var year = Year();
        _years.Seed(year);
        var draft = _closings.SeedDraft(year, _companyId, _tenantId, _retained.Id);

        var result = await CancelHandler().HandleAsync(
            new CancelPeriodClosingVoucherCommand(draft.Id, _companyId));

        Assert.False(result.IsSuccess);
        Assert.Equal(FiscalClosingErrorCodes.PeriodClosingInvalidTransition, result.Error!.Code);
    }

    // ---- Preview ----

    [Fact]
    public async Task Preview_MatchesLinesPersistedBySubmit()
    {
        var year = Year();
        SeedProfitYear(year);

        var previewHandler = new GetUnclosedPLBalancesQueryHandler(_years, _companies, _accounts, _closings);
        var preview = await previewHandler.HandleAsync(new GetUnclosedPLBalancesQuery(_companyId, year.Id));

        Assert.NotNull(preview);
        Assert.Equal(120_000m, preview!.Net);
        Assert.Equal(2, preview.Lines.Count);
        Assert.NotNull(preview.RetainedLine);
        Assert.Equal(120_000m, preview.RetainedLine!.Credit);

        var draft = _closings.SeedDraft(year, _companyId, _tenantId, _retained.Id);
        await SubmitHandler().HandleAsync(
            new SubmitPeriodClosingVoucherCommand(draft.Id, _companyId, draft.RowVersion, "s-9"));

        foreach (var line in preview.Lines)
        {
            var persisted = Assert.Single(
                _closings.Lines, l => l.AccountId == line.AccountId);
            Assert.Equal(line.Debit, persisted.Debit);
            Assert.Equal(line.Credit, persisted.Credit);
        }
    }

    // ---- In-memory doubles ----

    private sealed class FakeFiscalYearRepository : IFiscalYearRepository
    {
        public List<FiscalYear> Years { get; } = new();

        public void Seed(FiscalYear year) => Years.Add(year);

        public Task<FiscalYear?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Years.FirstOrDefault(x => x.Id == id));

        public Task<FiscalYear?> GetCoveringYearAsync(Guid companyId, DateOnly date, CancellationToken cancellationToken = default) =>
            Task.FromResult(Years.FirstOrDefault(x => x.CompanyId == companyId && x.StartDate <= date && x.EndDate >= date));

        public Task<bool> HasOverlapAsync(Guid companyId, DateOnly startDate, DateOnly endDate, Guid? excludeId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(Years.Any(x => x.CompanyId == companyId
                && (excludeId == null || x.Id != excludeId.Value)
                && x.StartDate <= endDate && startDate <= x.EndDate));

        public Task AddAsync(FiscalYear year, CancellationToken cancellationToken = default)
        {
            Years.Add(year);
            return Task.CompletedTask;
        }

        public void Update(FiscalYear year)
        {
        }

        public Task<List<FiscalYear>> GetPagedAsync(Guid companyId, bool? isClosed, int skip, int take, CancellationToken cancellationToken = default) =>
            Task.FromResult(Years.Where(x => x.CompanyId == companyId && (isClosed == null || x.IsClosed == isClosed.Value)).ToList());

        public Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default) => operation();

        public Task<T> ExecuteInSerializableTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default) => operation();

        public Task EnsurePostingDateInOpenYearAsync(Guid companyId, DateOnly postingDate, CancellationToken cancellationToken = default)
        {
            var covering = Years.FirstOrDefault(x => x.CompanyId == companyId && x.StartDate <= postingDate && x.EndDate >= postingDate);
            covering?.EnsurePostingAllowed(postingDate);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeClosingRepository : IPeriodClosingVoucherRepository
    {
        private readonly List<UnclosedPLBalance> _balances = new();
        private int _seq;

        public List<PeriodClosingVoucher> Vouchers { get; } = new();
        public List<GLEntry> GLEntries { get; } = new();
        public List<PeriodClosingVoucherLine> Lines { get; } = new();

        public PeriodClosingVoucher SeedDraft(FiscalYear year, Guid companyId, Guid tenantId, Guid retainedId)
        {
            var voucher = new PeriodClosingVoucher
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CompanyId = companyId,
                FiscalYearId = year.Id,
                VoucherNo = $"DRAFT-{year.StartDate.Year}-X",
                PostingDate = year.EndDate,
                RetainedEarningsAccountId = retainedId,
                DocumentStatus = DocumentStatus.Draft,
                RowVersion = new byte[] { 7, 7, 7 },
                CreatedAt = DateTimeOffset.UtcNow,
            };
            Vouchers.Add(voucher);
            return voucher;
        }

        public void SeedBalances(params UnclosedPLBalance[] balances)
        {
            _balances.Clear();
            _balances.AddRange(balances);
        }

        public Task<PeriodClosingVoucher?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Vouchers.FirstOrDefault(v => v.Id == id));

        public Task<PeriodClosingVoucher?> GetByIdempotencyKeyAsync(Guid companyId, string idempotencyKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(Vouchers.FirstOrDefault(v => v.CompanyId == companyId && v.IdempotencyKey == idempotencyKey));

        public Task AddAsync(PeriodClosingVoucher voucher, CancellationToken cancellationToken = default)
        {
            Vouchers.Add(voucher);
            return Task.CompletedTask;
        }

        public void Update(PeriodClosingVoucher voucher)
        {
        }

        public Task<List<PeriodClosingVoucher>> GetPagedAsync(Guid companyId, Guid? fiscalYearId, DocumentStatus? status, int skip, int take, CancellationToken cancellationToken = default) =>
            Task.FromResult(Vouchers.Where(v => v.CompanyId == companyId).ToList());

        public Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default) => operation();

        public Task<T> ExecuteInSerializableTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default) => operation();

        public Task<List<UnclosedPLBalance>> GetUnclosedPLBalancesAsync(Guid companyId, Guid fiscalYearId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_balances.ToList());

        public Task<bool> HasSubmittedCloseAsync(Guid companyId, Guid fiscalYearId, Guid? excludeVoucherId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(Vouchers.Any(v => v.CompanyId == companyId
                && v.FiscalYearId == fiscalYearId
                && v.DocumentStatus == DocumentStatus.Submitted
                && (excludeVoucherId == null || v.Id != excludeVoucherId.Value)));

        public Task<bool> HasDraftVoucherAsync(Guid fiscalYearId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Vouchers.Any(v => v.FiscalYearId == fiscalYearId && v.DocumentStatus == DocumentStatus.Draft));

        public Task<List<string>> GetVoucherNosOfYearAsync(Guid fiscalYearId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Vouchers.Where(v => v.FiscalYearId == fiscalYearId && v.DocumentStatus == DocumentStatus.Submitted).Select(v => v.VoucherNo).ToList());

        public Task<string> NextClosingVoucherNumberAsync(Guid companyId, FiscalYear fiscalYear, CancellationToken cancellationToken = default) =>
            Task.FromResult($"PCV-{fiscalYear.StartDate.Year}-{++_seq:D5}");

        public Task AddGLEntriesAsync(IEnumerable<GLEntry> entries, CancellationToken cancellationToken = default)
        {
            long id = GLEntries.Count + 1;
            foreach (var entry in entries)
            {
                entry.Id = id++;
                GLEntries.Add(entry);
            }

            return Task.CompletedTask;
        }

        public Task<List<GLEntry>> GetGLEntriesByVoucherAsync(Guid voucherId, CancellationToken cancellationToken = default) =>
            Task.FromResult(GLEntries.Where(x => x.VoucherId == voucherId).ToList());

        public Task AddLinesAsync(IEnumerable<PeriodClosingVoucherLine> lines, CancellationToken cancellationToken = default)
        {
            Lines.AddRange(lines);
            return Task.CompletedTask;
        }
    }
}
