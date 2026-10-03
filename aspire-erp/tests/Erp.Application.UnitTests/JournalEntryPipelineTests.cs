using Erp.Application.Features.GeneralLedger.Commands;
using Erp.Application.Features.GeneralLedger.Queries;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// tasks.md 2.3/2.4 - the Journal Entry posting pipeline exercised through the CQRS handlers
/// against in-memory repository doubles (Constitution I.2/I.3): the two-step workflow, plan.md §3's
/// canonical validation (balance -&gt; freeze -&gt; group account) and its "rejected submission
/// writes ZERO GLEntry rows" guarantee (spec AC-02), the gapless JV sequence (Constitution III.4),
/// the append-only reversal of spec AC-07 and the optional compare-and-swap RowVersion.
/// </summary>
public sealed class JournalEntryPipelineTests
{
    private static readonly DateOnly PostingDate = new(2026, 3, 2);

    /// <summary>A posting date INSIDE the frozen period used by the freeze tests.</summary>
    private static readonly DateOnly BackDated = new(2025, 6, 15);

    private static readonly DateOnly FrozenThrough = new(2025, 12, 31);

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly FakeCompanyRepository _companies = new();
    private readonly FakeAccountRepository _accounts = new();
    private readonly FakeJournalRepository _journals = new();

    private readonly Account _cash;    // 1110 - leaf, active, same company
    private readonly Account _sales;   // 4010 - leaf, active, same company
    private readonly Account _group;   // 1000 - GROUP account (spec AC-03)

    public JournalEntryPipelineTests()
    {
        _companies.Company = new Company
        {
            Id = _companyId,
            TenantId = Guid.NewGuid(),
            Name = "Acme Industrial",
        };

        _cash = LeafAccount("1110", "Cash at Bank");
        _sales = LeafAccount("4010", "Sales Revenue");
        _group = new Account
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CompanyId = _companyId,
            AccountCode = "1000",
            AccountName = "Assets",
            IsGroup = true,
            IsActive = true,
        };

        _accounts.Seed(_cash, _sales, _group);
    }

    private Account LeafAccount(string code, string name) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        CompanyId = _companyId,
        AccountCode = code,
        AccountName = name,
        IsGroup = false,
        IsActive = true,
    };

    private CreateJournalEntryCommandHandler CreateHandler() => new(_companies, _journals);

    private SubmitJournalEntryCommandHandler SubmitHandler() => new(_companies, _accounts, _journals);

    private CancelJournalEntryCommandHandler CancelHandler() => new(_companies, _accounts, _journals);

    /// <summary>Balanced two-line voucher payload (debit cash / credit sales).</summary>
    private static CreateJournalEntryCommand BalancedCommand(
        Guid companyId, decimal amount = 1000m, DateOnly? postingDate = null) =>
        new(
            companyId,
            postingDate ?? PostingDate,
            JournalEntryType.Standard,
            "Operating entry",
            new[]
            {
                new CreateJournalEntryLine(Guid.NewGuid(), amount, 0m),
                new CreateJournalEntryLine(Guid.NewGuid(), 0m, amount),
            });

    /// <summary>
    /// Seeds a voucher directly (optionally with its ledger rows already posted) - submit and
    /// cancel tests start from a persisted row, not from the create handler.
    /// </summary>
    private JournalEntry SeedEntry(
        JournalEntryStatus status,
        DateOnly postingDate,
        Guid? accountId1 = null,
        Guid? accountId2 = null,
        byte[]? rowVersion = null,
        DateTimeOffset? createdAt = null)
    {
        var entry = new JournalEntry
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CompanyId = _companyId,
            VoucherNo = "JV-2026-00042",
            PostingDate = postingDate,
            Status = status,
            UserRemark = "Seeded voucher",
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
            RowVersion = rowVersion ?? new byte[] { 0x01, 0x02, 0x03 },
            Lines = new List<JournalEntryLine>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    LineNumber = 1,
                    AccountId = accountId1 ?? _cash.Id,
                    Debit = 1000m,
                    Credit = 0m,
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    LineNumber = 2,
                    AccountId = accountId2 ?? _sales.Id,
                    Debit = 0m,
                    Credit = 1000m,
                },
            },
        };

        _journals.Seed(entry);
        return entry;
    }

    // ------------------------------------------------------------------------- create (step one)

    [Fact]
    public async Task Create_ValidLines_PersistsDraftWithGaplessVoucherAndZeroLedgerRows()
    {
        var first = await CreateHandler().HandleAsync(BalancedCommand(_companyId));
        var second = await CreateHandler().HandleAsync(BalancedCommand(_companyId));

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);

        // Constitution III.4: gapless JV-YYYY-NNNNN, assigned at CREATE (Draft carries its number).
        Assert.Equal("JV-2026-00001", first.Value!.VoucherNo);
        Assert.Equal("JV-2026-00002", second.Value!.VoucherNo);

        Assert.Equal(JournalEntryStatus.Draft, first.Value.Status);
        Assert.Equal(2, first.Value.Lines.Count);
        Assert.Equal(1, first.Value.Lines[0].LineNumber);
        Assert.Equal("Operating entry", first.Value.UserRemark);

        // Step one writes the header/lines ONLY - no GLEntry row exists before submit (AC-01/AC-02).
        Assert.Empty(_journals.GlEntries);
        Assert.Equal(2, _journals.TransactionCount); // one number+insert transaction per create
    }

    [Fact]
    public async Task Create_ImbalancedLines_StillPersistsDraft()
    {
        // spec AC-02 starts FROM this draft ("debits $1,000.00 and credits $995.00"): balance is
        // a SUBMIT rule (plan §3), never a create rule - otherwise the scenario could not exist.
        var command = new CreateJournalEntryCommand(
            _companyId,
            PostingDate,
            JournalEntryType.Standard,
            null,
            new[]
            {
                new CreateJournalEntryLine(Guid.NewGuid(), 1000m, 0m),
                new CreateJournalEntryLine(Guid.NewGuid(), 0m, 995m),
            });

        var result = await CreateHandler().HandleAsync(command);

        Assert.True(result.IsSuccess);
        Assert.Equal(JournalEntryStatus.Draft, result.Value!.Status);
        Assert.Empty(_journals.GlEntries);
    }

    [Fact]
    public async Task Create_NoLines_FailsWithNoLines()
    {
        var result = await CreateHandler().HandleAsync(
            new CreateJournalEntryCommand(_companyId, PostingDate, Lines: null));

        Assert.False(result.IsSuccess);
        Assert.Equal(JournalErrorCodes.NoLines, result.Error!.Code);
        Assert.Empty(_journals.Entries);
        Assert.Empty(_journals.GlEntries);
    }

    [Fact]
    public async Task Create_UnknownCompany_FailsWithCompanyNotFound()
    {
        _companies.Company = null; // tenant has no such company (pattern of StockPostingServiceTests)

        var result = await CreateHandler().HandleAsync(BalancedCommand(Guid.NewGuid()));

        Assert.False(result.IsSuccess);
        Assert.Equal(JournalErrorCodes.CompanyNotFound, result.Error!.Code);
        Assert.Empty(_journals.Entries);
    }

    [Fact]
    public async Task Create_NegativeLineAmount_FailsWithInvalidAmount()
    {
        var command = new CreateJournalEntryCommand(
            _companyId,
            PostingDate,
            Lines: new[]
            {
                new CreateJournalEntryLine(Guid.NewGuid(), -1m, 0m),
            });

        var result = await CreateHandler().HandleAsync(command);

        Assert.False(result.IsSuccess);
        Assert.Equal(JournalErrorCodes.InvalidAmount, result.Error!.Code);
        Assert.Empty(_journals.Entries);
    }

    [Fact]
    public async Task Create_BothSidesZeroLine_FailsWithInvalidAmount()
    {
        var command = new CreateJournalEntryCommand(
            _companyId,
            PostingDate,
            Lines: new[] { new CreateJournalEntryLine(Guid.NewGuid(), 0m, 0m) });

        var result = await CreateHandler().HandleAsync(command);

        Assert.False(result.IsSuccess);
        Assert.Equal(JournalErrorCodes.InvalidAmount, result.Error!.Code);
        Assert.Empty(_journals.Entries);
    }

    // ------------------------------------------------------------------------- submit (step two)

    [Fact]
    public async Task Submit_BalancedDraft_AppendsMirroredLedgerRowsInsideOneTransaction()
    {
        var partyId = Guid.NewGuid();
        var costCenterId = Guid.NewGuid();
        var created = await CreateHandler().HandleAsync(new CreateJournalEntryCommand(
            _companyId,
            PostingDate,
            JournalEntryType.Standard,
            "Operating entry",
            new[]
            {
                new CreateJournalEntryLine(_cash.Id, 1000m, 0m, "Bank", partyId, costCenterId),
                new CreateJournalEntryLine(_sales.Id, 0m, 1000m),
            }));

        var result = await SubmitHandler().HandleAsync(
            new SubmitJournalEntryCommand(_companyId, created.Value!.Id));

        // spec AC-01: status becomes Submitted.
        Assert.True(result.IsSuccess);
        Assert.Equal(JournalEntryStatus.Submitted, result.Value!.Status);

        // tasks.md 2.4: N balanced rows appended ATOMICALLY (create txn + ONE submit txn).
        Assert.Equal(2, _journals.TransactionCount);
        Assert.Equal(2, _journals.GlEntries.Count);

        var debitRow = _journals.GlEntries.Single(g => g.Debit > 0m);
        var creditRow = _journals.GlEntries.Single(g => g.Credit > 0m);

        foreach (var row in _journals.GlEntries)
        {
            Assert.Equal("JournalEntry", row.VoucherType);
            Assert.Equal(created.Value.Id, row.VoucherId);
            Assert.Equal(created.Value.VoucherNo, row.VoucherNo);
            Assert.Equal(PostingDate, row.PostingDate);
            Assert.False(row.IsCancelled);

            // plan §2 account-currency pair: single-currency posting mirrors the ledger amount.
            Assert.Equal(row.Debit, row.DebitInAccountCurrency);
            Assert.Equal(row.Credit, row.CreditInAccountCurrency);
            Assert.Equal("USD", row.AccountCurrency);
        }

        // Constitution III.1 / plan §3 on the written rows.
        Assert.Equal(_journals.GlEntries.Sum(g => g.Debit), _journals.GlEntries.Sum(g => g.Credit));
        Assert.Equal(1000m, debitRow.Debit);
        Assert.Equal(1000m, creditRow.Credit);

        // Line dimensions and the header remark carried over to the ledger row.
        Assert.Equal("Bank", debitRow.PartyType);
        Assert.Equal(partyId, debitRow.PartyId);
        Assert.Equal(costCenterId, debitRow.CostCenterId);
        Assert.Equal("Operating entry", debitRow.Remarks);
        Assert.Equal(created.Value.Id, debitRow.VoucherId);
    }

    [Fact]
    public async Task Submit_ImbalancedDraft_FailsWithDoubleEntryImbalanceAndZeroLedgerRows()
    {
        // spec AC-02 Gherkin: $1,000.00 debit vs $995.00 credit -> rejection, ZERO rows.
        var created = await CreateHandler().HandleAsync(new CreateJournalEntryCommand(
            _companyId,
            PostingDate,
            Lines: new[]
            {
                new CreateJournalEntryLine(_cash.Id, 1000m, 0m),
                new CreateJournalEntryLine(_sales.Id, 0m, 995m),
            }));

        var result = await SubmitHandler().HandleAsync(
            new SubmitJournalEntryCommand(_companyId, created.Value!.Id));

        Assert.False(result.IsSuccess);
        Assert.Equal(StockErrorCodes.DoubleEntryImbalance, result.Error!.Code);

        // AC-02 "zero records are written to GLEntry" - the transaction rolled everything back
        // AND the status never moved out of Draft.
        Assert.Empty(_journals.GlEntries);
        Assert.Equal(JournalEntryStatus.Draft, _journals.Entries.Single().Status);
    }

    [Fact]
    public async Task Submit_GroupAccount_FailsWithPostingToGroupAccountProhibitedAndZeroLedgerRows()
    {
        // spec AC-03 Gherkin: line targets group account 1000 -> rejection, ZERO rows.
        var created = await CreateHandler().HandleAsync(new CreateJournalEntryCommand(
            _companyId,
            PostingDate,
            Lines: new[]
            {
                new CreateJournalEntryLine(_group.Id, 1000m, 0m),
                new CreateJournalEntryLine(_sales.Id, 0m, 1000m),
            }));

        var result = await SubmitHandler().HandleAsync(
            new SubmitJournalEntryCommand(_companyId, created.Value!.Id));

        Assert.False(result.IsSuccess);
        Assert.Equal(AccountingErrorCodes.PostingToGroupAccountProhibited, result.Error!.Code);
        Assert.Empty(_journals.GlEntries);
        Assert.Equal(JournalEntryStatus.Draft, _journals.Entries.Single().Status);
    }

    [Fact]
    public async Task Submit_FrozenPeriod_FailsWithFiscalPeriodLockedAndZeroLedgerRows()
    {
        // spec AC-04: freeze through 2025-12-31, voucher dated 2025-06-15 -> blocked at the
        // FIRST data gate, before balance/accounts/state are even touched.
        _companies.Company!.FrozenAccountsDate = FrozenThrough;
        var created = await CreateHandler().HandleAsync(
            BalancedCommand(_companyId, postingDate: BackDated));

        var result = await SubmitHandler().HandleAsync(
            new SubmitJournalEntryCommand(_companyId, created.Value!.Id));

        Assert.False(result.IsSuccess);
        Assert.Equal(AccountingErrorCodes.FiscalPeriodLocked, result.Error!.Code);
        Assert.Empty(_journals.GlEntries);
        Assert.Equal(JournalEntryStatus.Draft, _journals.Entries.Single().Status);
    }

    [Fact]
    public async Task Submit_UnknownEntry_FailsWithJournalEntryNotFound()
    {
        var result = await SubmitHandler().HandleAsync(
            new SubmitJournalEntryCommand(_companyId, Guid.NewGuid()));

        Assert.False(result.IsSuccess);
        Assert.Equal(JournalErrorCodes.JournalEntryNotFound, result.Error!.Code);
        Assert.Empty(_journals.GlEntries);
    }

    [Fact]
    public async Task Submit_EntryOfAnotherCompany_FailsWithJournalEntryNotFound()
    {
        var entry = SeedEntry(JournalEntryStatus.Draft, PostingDate);

        var result = await SubmitHandler().HandleAsync(
            new SubmitJournalEntryCommand(Guid.NewGuid(), entry.Id)); // wrong company route

        Assert.False(result.IsSuccess);
        Assert.Equal(JournalErrorCodes.JournalEntryNotFound, result.Error!.Code);
        Assert.Empty(_journals.GlEntries);
    }

    [Fact]
    public async Task Submit_AlreadySubmitted_FailsWithInvalidStatusTransition()
    {
        var entry = SeedEntry(JournalEntryStatus.Submitted, PostingDate);

        var result = await SubmitHandler().HandleAsync(
            new SubmitJournalEntryCommand(_companyId, entry.Id));

        Assert.False(result.IsSuccess);
        Assert.Equal(JournalErrorCodes.InvalidStatusTransition, result.Error!.Code);
        Assert.Empty(_journals.GlEntries); // no duplicate rows for a double submit
    }

    [Fact]
    public async Task Submit_StaleClientRowVersion_FailsWithConcurrencyConflict()
    {
        var entry = SeedEntry(JournalEntryStatus.Draft, PostingDate);

        var result = await SubmitHandler().HandleAsync(
            new SubmitJournalEntryCommand(_companyId, entry.Id, new byte[] { 0xEE, 0xFF }));

        Assert.False(result.IsSuccess);
        Assert.Equal("concurrency_conflict", result.Error!.Code);
        Assert.Empty(_journals.GlEntries);
        Assert.Equal(JournalEntryStatus.Draft, entry.Status);
    }

    [Fact]
    public async Task Submit_MatchingClientRowVersion_Succeeds()
    {
        var entry = SeedEntry(JournalEntryStatus.Draft, PostingDate);

        var result = await SubmitHandler().HandleAsync(
            new SubmitJournalEntryCommand(_companyId, entry.Id, new byte[] { 0x01, 0x02, 0x03 }));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, _journals.GlEntries.Count);
    }

    [Fact]
    public async Task Submit_UnknownAccount_FailsWithAccountNotFoundAndZeroLedgerRows()
    {
        var entry = SeedEntry(
            JournalEntryStatus.Draft, PostingDate, accountId1: Guid.NewGuid());

        var result = await SubmitHandler().HandleAsync(
            new SubmitJournalEntryCommand(_companyId, entry.Id));

        Assert.False(result.IsSuccess);
        Assert.Equal(JournalErrorCodes.AccountNotFound, result.Error!.Code);
        Assert.Empty(_journals.GlEntries);
        Assert.Equal(JournalEntryStatus.Draft, entry.Status);
    }

    [Fact]
    public async Task Submit_InactiveAccount_FailsWithInvalidGlAccountAndZeroLedgerRows()
    {
        var inactive = LeafAccount("5110", "Retained Earnings");
        inactive.IsActive = false;
        _accounts.Seed(inactive);

        var entry = SeedEntry(JournalEntryStatus.Draft, PostingDate, accountId1: inactive.Id);

        var result = await SubmitHandler().HandleAsync(
            new SubmitJournalEntryCommand(_companyId, entry.Id));

        Assert.False(result.IsSuccess);
        Assert.Equal(JournalErrorCodes.InvalidGlAccount, result.Error!.Code);
        Assert.Empty(_journals.GlEntries);
    }

    [Fact]
    public async Task Submit_ForeignCompanyAccount_FailsWithInvalidGlAccountAndZeroLedgerRows()
    {
        var foreign = LeafAccount("1110", "Foreign cash");
        foreign.CompanyId = Guid.NewGuid();
        _accounts.Seed(foreign);

        var entry = SeedEntry(JournalEntryStatus.Draft, PostingDate, accountId1: foreign.Id);

        var result = await SubmitHandler().HandleAsync(
            new SubmitJournalEntryCommand(_companyId, entry.Id));

        Assert.False(result.IsSuccess);
        Assert.Equal(JournalErrorCodes.InvalidGlAccount, result.Error!.Code);
        Assert.Empty(_journals.GlEntries);
    }

    // ------------------------------------------------------------------------- cancel (spec AC-07)

    [Fact]
    public async Task Cancel_SubmittedEntry_AppendsSwappedReversalAndKeepsOriginals()
    {
        var entry = SeedEntry(JournalEntryStatus.Submitted, PostingDate);

        // The originals, snapshot BEFORE the cancellation (Constitution III.2: never touched).
        // GLEntry.Id is a store-generated long identity - never set it from code.
        var originalDebit = new GLEntry
        {
            CompanyId = _companyId,
            PostingDate = PostingDate,
            AccountId = _cash.Id,
            Debit = 1000m,
            Credit = 0m,
            DebitInAccountCurrency = 1000m,
            VoucherType = "JournalEntry",
            VoucherNo = entry.VoucherNo,
            VoucherId = entry.Id,
            IsCancelled = false,
            Remarks = "Seeded voucher",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        var originalCredit = new GLEntry
        {
            CompanyId = _companyId,
            PostingDate = PostingDate,
            AccountId = _sales.Id,
            Debit = 0m,
            Credit = 1000m,
            CreditInAccountCurrency = 1000m,
            VoucherType = "JournalEntry",
            VoucherNo = entry.VoucherNo,
            VoucherId = entry.Id,
            IsCancelled = false,
            Remarks = "Seeded voucher",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        await _journals.AddGlEntriesAsync(new[] { originalDebit, originalCredit });

        // Snapshot of the ORIGINALS taken before the cancellation: the append-only guarantee
        // (Constitution III.2) means these values must be byte-identical afterwards.
        var debitBefore = (originalDebit.Debit, originalDebit.Credit, originalDebit.IsCancelled, originalDebit.Remarks);
        var creditBefore = (originalCredit.Debit, originalCredit.Credit, originalCredit.IsCancelled, originalCredit.Remarks);

        var result = await CancelHandler().HandleAsync(
            new CancelJournalEntryCommand(_companyId, entry.Id));

        // spec AC-07: header status transitions to Cancelled.
        Assert.True(result.IsSuccess);
        Assert.Equal(JournalEntryStatus.Cancelled, result.Value!.Status);

        Assert.Equal(4, _journals.GlEntries.Count);
        var reversals = _journals.GlEntries.Skip(2).ToList();

        // Reversal rows: SWAPPED amounts, same voucher identity, ORIGINAL posting date,
        // IsCancelled = true.
        foreach (var original in new[] { originalDebit, originalCredit })
        {
            var reversal = Assert.Single(reversals, r => r.AccountId == original.AccountId);
            Assert.Equal(original.Credit, reversal.Debit);
            Assert.Equal(original.Debit, reversal.Credit);
            Assert.Equal(original.VoucherId, reversal.VoucherId);
            Assert.Equal(original.VoucherNo, reversal.VoucherNo);
            Assert.Equal(original.PostingDate, reversal.PostingDate);
            Assert.True(reversal.IsCancelled);
            Assert.Contains(entry.VoucherNo, reversal.Remarks);
            Assert.StartsWith("Reversal of JournalEntry", reversal.Remarks);
        }

        // ...and the originals themselves were NEVER mutated (the flag lives on the reversal).
        Assert.Equal(debitBefore, (originalDebit.Debit, originalDebit.Credit, originalDebit.IsCancelled, originalDebit.Remarks));
        Assert.Equal(creditBefore, (originalCredit.Debit, originalCredit.Credit, originalCredit.IsCancelled, originalCredit.Remarks));

        // Trial-balance neutrality: debits == credits across originals + reversals.
        Assert.Equal(_journals.GlEntries.Sum(g => g.Debit), _journals.GlEntries.Sum(g => g.Credit));
    }

    [Fact]
    public async Task Cancel_DraftEntry_FailsWithInvalidStatusTransitionAndZeroLedgerRows()
    {
        var entry = SeedEntry(JournalEntryStatus.Draft, PostingDate);

        var result = await CancelHandler().HandleAsync(
            new CancelJournalEntryCommand(_companyId, entry.Id));

        Assert.False(result.IsSuccess);
        Assert.Equal(JournalErrorCodes.InvalidStatusTransition, result.Error!.Code);
        Assert.Empty(_journals.GlEntries);
        Assert.Equal(JournalEntryStatus.Draft, entry.Status);
    }

    [Fact]
    public async Task Cancel_AlreadyCancelled_FailsWithInvalidStatusTransition()
    {
        var entry = SeedEntry(JournalEntryStatus.Cancelled, PostingDate);

        var result = await CancelHandler().HandleAsync(
            new CancelJournalEntryCommand(_companyId, entry.Id));

        Assert.False(result.IsSuccess);
        Assert.Equal(JournalErrorCodes.InvalidStatusTransition, result.Error!.Code);
        Assert.Empty(_journals.GlEntries);
    }

    [Fact]
    public async Task Cancel_FrozenOriginalPeriod_FailsWithFiscalPeriodLockedAndZeroLedgerRows()
    {
        // spec AC-04 wording covers "posting, modification, OR CANCELLATION": the reversal keeps
        // the ORIGINAL PostingDate, so a frozen period blocks the cancellation too.
        _companies.Company!.FrozenAccountsDate = FrozenThrough;
        var entry = SeedEntry(JournalEntryStatus.Submitted, BackDated);

        var result = await CancelHandler().HandleAsync(
            new CancelJournalEntryCommand(_companyId, entry.Id));

        Assert.False(result.IsSuccess);
        Assert.Equal(AccountingErrorCodes.FiscalPeriodLocked, result.Error!.Code);
        Assert.Empty(_journals.GlEntries);
        Assert.Equal(JournalEntryStatus.Submitted, entry.Status);
    }

    [Fact]
    public async Task Cancel_StaleClientRowVersion_FailsWithConcurrencyConflict()
    {
        var entry = SeedEntry(JournalEntryStatus.Submitted, PostingDate);

        var result = await CancelHandler().HandleAsync(
            new CancelJournalEntryCommand(_companyId, entry.Id, new byte[] { 0xEE, 0xFF }));

        Assert.False(result.IsSuccess);
        Assert.Equal("concurrency_conflict", result.Error!.Code);
        Assert.Empty(_journals.GlEntries);
        Assert.Equal(JournalEntryStatus.Submitted, entry.Status);
    }

    [Fact]
    public async Task Cancel_UnknownEntry_FailsWithJournalEntryNotFound()
    {
        var result = await CancelHandler().HandleAsync(
            new CancelJournalEntryCommand(_companyId, Guid.NewGuid()));

        Assert.False(result.IsSuccess);
        Assert.Equal(JournalErrorCodes.JournalEntryNotFound, result.Error!.Code);
        Assert.Empty(_journals.GlEntries);
    }

    // ------------------------------------------------------------------------- read side

    [Fact]
    public async Task GetById_ExistingEntry_ReturnsDtoWithBase64RowVersionAndLines()
    {
        var entry = SeedEntry(JournalEntryStatus.Draft, PostingDate);

        var dto = await new GetJournalEntryQueryHandler(_journals)
            .HandleAsync(new GetJournalEntryQuery(_companyId, entry.Id));

        Assert.NotNull(dto);
        Assert.Equal(entry.Id, dto!.Id);
        Assert.Equal(entry.VoucherNo, dto.VoucherNo);
        Assert.Equal(JournalEntryStatus.Draft, dto.Status);
        Assert.Equal(Convert.ToBase64String(entry.RowVersion), dto.RowVersion);
        Assert.Equal(2, dto.Lines.Count);
        Assert.Equal(1000m, dto.Lines[0].Debit);
        Assert.Equal(1000m, dto.Lines[1].Credit);
    }

    [Fact]
    public async Task GetById_UnknownOrForeignCompanyEntry_ReturnsNull()
    {
        var entry = SeedEntry(JournalEntryStatus.Draft, PostingDate);
        var handler = new GetJournalEntryQueryHandler(_journals);

        Assert.Null(await handler.HandleAsync(new GetJournalEntryQuery(_companyId, Guid.NewGuid())));
        Assert.Null(await handler.HandleAsync(new GetJournalEntryQuery(Guid.NewGuid(), entry.Id)));
    }

    [Fact]
    public async Task GetRecent_ReturnsNewestFirstForTheCompanyOnly()
    {
        // Explicit timestamps: DateTimeOffset.UtcNow resolution is not guaranteed to advance
        // between two calls on every platform, and "newest first" must be deterministic.
        var older = SeedEntry(
            JournalEntryStatus.Draft,
            PostingDate,
            createdAt: new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero));
        var newer = SeedEntry(
            JournalEntryStatus.Draft,
            PostingDate,
            createdAt: new DateTimeOffset(2026, 3, 2, 12, 0, 0, TimeSpan.Zero));

        var handler = new GetJournalEntriesQueryHandler(_journals);
        var list = await handler.HandleAsync(new GetJournalEntriesQuery(_companyId, Limit: 10));

        Assert.Equal(2, list.Count);
        Assert.Equal(newer.Id, list[0].Id);
        Assert.Equal(older.Id, list[1].Id);

        // An empty company returns an empty list (the API turns that into 200 []).
        Assert.Empty(await handler.HandleAsync(new GetJournalEntriesQuery(Guid.NewGuid())));

        // Limit: 0 falls back to the documented default (50), never to "no rows".
        Assert.Equal(2, (await handler.HandleAsync(new GetJournalEntriesQuery(_companyId, 0))).Count);
    }
}
