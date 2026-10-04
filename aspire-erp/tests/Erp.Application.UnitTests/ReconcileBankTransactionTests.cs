using Erp.Application.Features.Banking.Commands;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Task 6.4 acceptance through the CQRS handler against in-memory doubles: scenario BN-03
/// (single PaymentEntry -&gt; Reconciled + ClearanceDate both sides, PostingDate intact,
/// $0.00 difference), BN-04 multi-voucher allocation, the GLEntry-counterpart path with
/// byte-identical GL rows, every rejection with zero writes, BN-07 concurrency, and the
/// BN-06 un-reconcile reversal.
/// </summary>
public sealed class ReconcileBankTransactionTests
{
    private static readonly DateOnly StatementDate = new(2026, 10, 2);
    private static readonly DateOnly PaymentDate = new(2026, 9, 30);

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _accountId = Guid.NewGuid();
    private readonly FakeBankRepository _bank = new();
    private readonly FakeGLEntryRepository _ledger = new();

    public ReconcileBankTransactionTests()
    {
        _bank.SeedAccount(new BankAccount
        {
            Id = _accountId,
            TenantId = _tenantId,
            CompanyId = _companyId,
            AccountName = "Main Checking",
            BankName = "Acme Bank",
            AccountNumber = "0012345678",
            GLAccountId = Guid.NewGuid(),
        });
    }

    private BankTransaction Deposit(decimal amount, BankTransactionStatus status = BankTransactionStatus.Unreconciled)
    {
        var transaction = new BankTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            CompanyId = _companyId,
            BankAccountId = _accountId,
            TransactionDate = StatementDate,
            Deposit = amount,
            Withdrawal = 0m,
            Description = "STRIPE PAYOUT",
            Status = status,
            RowVersion = new byte[] { 0x01 },
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _bank.SeedTransaction(transaction);
        return transaction;
    }

    private PaymentEntry Voucher(decimal paidAmount)
    {
        var entry = new PaymentEntry
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            CompanyId = _companyId,
            BankAccountId = _accountId,
            PaymentType = PaymentType.Receive,
            PaymentDate = PaymentDate,
            PaidAmount = paidAmount,
            Status = PaymentStatus.Unreconciled,
            RowVersion = new byte[] { 0x01 },
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _bank.SeedPaymentEntry(entry);
        return entry;
    }

    private ReconcileBankTransactionCommandHandler Handler() => new(_bank, _ledger);

    private static ReconcileBankTransactionCommand Reconcile(
        Guid companyId,
        Guid transactionId,
        params ReconciliationLine[] lines) =>
        new(companyId, transactionId, lines);

    private static ReconciliationLine PaySlice(Guid entryId, decimal amount) =>
        new(entryId, null, amount);

    // ------------------------------------------------------------ BN-03 single voucher

    /// <summary>
    /// Scenario BN-03: $1,000 deposit vs $1,000 voucher -&gt; Reconciled both sides, clearance
    /// stamped as the STATEMENT date, difference $0.00, voucher PaymentDate untouched (BN-03).
    /// </summary>
    [Fact]
    public async Task Reconcile_DepositAgainstEqualVoucher_ReconcilesBothSidesWithClearance()
    {
        var transaction = Deposit(1000m);
        var entry = Voucher(1000m);
        var glBefore = _bank.AddedGlEntries.Count;

        var result = await Handler().HandleAsync(
            Reconcile(_companyId, transaction.Id, PaySlice(entry.Id, 1000m)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var summary = result.Value!;
        Assert.Equal(transaction.Id, summary.BankTransactionId);
        Assert.Equal(1000m, summary.AllocatedAmount);
        Assert.Equal(StatementDate, summary.ClearanceDate);
        var slice = Assert.Single(summary.Lines);
        Assert.Equal("PaymentEntry", slice.CounterpartType);
        Assert.Equal(entry.Id, slice.CounterpartId);
        Assert.Equal(1000m, slice.Amount);

        Assert.Equal(BankTransactionStatus.Reconciled, transaction.Status);
        Assert.Equal(1000m, transaction.AllocatedAmount);
        Assert.Equal(StatementDate, transaction.ClearanceDate);

        Assert.Equal(PaymentStatus.Reconciled, entry.Status);
        Assert.Equal(StatementDate, entry.ClearanceDate);

        // BN-03: the voucher's accounting date is NEVER rewritten by clearance stamping.
        Assert.Equal(PaymentDate, entry.PaymentDate);

        // Difference is $0.00: |Deposit - Withdrawal| - allocated == 0.
        Assert.Equal(0m, Math.Abs(transaction.Deposit - transaction.Withdrawal) - transaction.AllocatedAmount);

        Assert.Single(_bank.Links);
        Assert.Equal(1, _bank.TransactionCount); // ONE transaction
        Assert.Equal(glBefore, _bank.AddedGlEntries.Count); // zero GL writes on this path
    }

    /// <summary>A rule-Matched line reconciles exactly like an Unreconciled one.</summary>
    [Fact]
    public async Task Reconcile_MatchedLine_Reconciles()
    {
        var transaction = Deposit(1000m, BankTransactionStatus.Matched);
        var entry = Voucher(1000m);

        var result = await Handler().HandleAsync(
            Reconcile(_companyId, transaction.Id, PaySlice(entry.Id, 1000m)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BankTransactionStatus.Reconciled, transaction.Status);
    }

    // ------------------------------------------------------------ BN-04 multi-voucher

    /// <summary>
    /// Invariant BN-04: one $1,000 line reconciles against TWO vouchers ($600 + $400).
    /// </summary>
    [Fact]
    public async Task Reconcile_OneLineAgainstTwoVouchers_SumsExactlyAndReconcilesAll()
    {
        var transaction = Deposit(1000m);
        var first = Voucher(600m);
        var second = Voucher(400m);

        var result = await Handler().HandleAsync(
            Reconcile(_companyId, transaction.Id, PaySlice(first.Id, 600m), PaySlice(second.Id, 400m)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Lines.Count);
        Assert.Equal(BankTransactionStatus.Reconciled, transaction.Status);
        Assert.Equal(1000m, transaction.AllocatedAmount);
        Assert.Equal(PaymentStatus.Reconciled, first.Status);
        Assert.Equal(PaymentStatus.Reconciled, second.Status);
        Assert.Equal(StatementDate, first.ClearanceDate);
        Assert.Equal(StatementDate, second.ClearanceDate);
        Assert.Equal(2, _bank.Links.Count);
        Assert.Empty(_bank.AddedGlEntries);
    }

    /// <summary>
    /// BN-04 off-by-cent: $999.99 vs a $1,000 line fails with a typed 400 and zero writes.
    /// </summary>
    [Fact]
    public async Task Reconcile_OffByOneCent_FailsWithAmountMismatchAndWritesNothing()
    {
        var transaction = Deposit(1000m);
        var entry = Voucher(1000m);

        var result = await Handler().HandleAsync(
            Reconcile(_companyId, transaction.Id, PaySlice(entry.Id, 999.99m)),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BankingErrorCodes.ReconciliationAmountMismatch, result.Error!.Code);
        Assert.Equal(BankTransactionStatus.Unreconciled, transaction.Status);
        Assert.Equal(0m, transaction.AllocatedAmount);
        Assert.Null(transaction.ClearanceDate);
        Assert.Equal(PaymentStatus.Unreconciled, entry.Status);
        Assert.Null(entry.ClearanceDate);
        Assert.Empty(_bank.Links);
        Assert.Empty(_bank.AddedGlEntries);
    }

    // ------------------------------------------------------------ GLEntry counterpart

    private Guid SeedGlVoucher(decimal total, Guid? companyId = null)
    {
        var voucherId = Guid.NewGuid();
        var owner = companyId ?? _companyId;
        var debitAccount = Guid.NewGuid();
        var creditAccount = Guid.NewGuid();

        _ledger.Seed(
            new GLEntry
            {
                Id = 1,
                TenantId = _tenantId,
                CompanyId = owner,
                PostingDate = PaymentDate,
                AccountId = debitAccount,
                Debit = total,
                Credit = 0m,
                DebitInAccountCurrency = total,
                CreditInAccountCurrency = 0m,
                VoucherType = "JournalEntry",
                VoucherNo = "JV-2026-00001",
                VoucherId = voucherId,
                CreatedAt = DateTimeOffset.UtcNow,
            },
            new GLEntry
            {
                Id = 2,
                TenantId = _tenantId,
                CompanyId = owner,
                PostingDate = PaymentDate,
                AccountId = creditAccount,
                Debit = 0m,
                Credit = total,
                DebitInAccountCurrency = 0m,
                CreditInAccountCurrency = total,
                VoucherType = "JournalEntry",
                VoucherNo = "JV-2026-00001",
                VoucherId = voucherId,
                CreatedAt = DateTimeOffset.UtcNow,
            });

        return voucherId;
    }

    private static List<(long Id, decimal Debit, decimal Credit, bool IsCancelled)> GlSnapshot(
        FakeGLEntryRepository ledger) =>
        ledger.Rows.Select(r => (r.Id, r.Debit, r.Credit, r.IsCancelled)).ToList();

    /// <summary>
    /// GLEntry path: a $15 bank-fee withdrawal reconciles against its $15 journal voucher -
    /// the link is created and the GL rows stay byte-identical (append-only, zero writes).
    /// </summary>
    [Fact]
    public async Task Reconcile_WithdrawalAgainstGlVoucher_CreatesLinkAndLeavesLedgerUntouched()
    {
        var transaction = new BankTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            CompanyId = _companyId,
            BankAccountId = _accountId,
            TransactionDate = StatementDate,
            Deposit = 0m,
            Withdrawal = 15m,
            Description = "MONTHLY BANK FEE",
            Status = BankTransactionStatus.Unreconciled,
            RowVersion = new byte[] { 0x01 },
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _bank.SeedTransaction(transaction);
        var voucherId = SeedGlVoucher(15m);
        var glBefore = GlSnapshot(_ledger);

        var result = await Handler().HandleAsync(
            Reconcile(_companyId, transaction.Id, new ReconciliationLine(null, voucherId, 15m)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var slice = Assert.Single(result.Value!.Lines);
        Assert.Equal("GLEntry", slice.CounterpartType);
        Assert.Equal(voucherId, slice.CounterpartId);
        Assert.Equal(BankTransactionStatus.Reconciled, transaction.Status);
        Assert.Equal(15m, transaction.AllocatedAmount);
        Assert.Single(_bank.Links);

        // GLEntry rows byte-identical: the reconcile appended nothing and mutated nothing.
        Assert.Equal(glBefore, GlSnapshot(_ledger));
        Assert.Empty(_bank.AddedGlEntries);
    }

    /// <summary>Unknown GL voucher: typed 404 with zero writes.</summary>
    [Fact]
    public async Task Reconcile_UnknownGlVoucher_FailsWithGlVoucherNotFoundAndWritesNothing()
    {
        var transaction = Deposit(100m);

        var result = await Handler().HandleAsync(
            Reconcile(_companyId, transaction.Id, new ReconciliationLine(null, Guid.NewGuid(), 100m)),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BankingErrorCodes.GlVoucherNotFound, result.Error!.Code);
        Assert.Equal(BankTransactionStatus.Unreconciled, transaction.Status);
        Assert.Empty(_bank.Links);
    }

    /// <summary>
    /// GL voucher of ANOTHER company reads as NOT FOUND (company-scoped read, no leak) with
    /// zero writes.
    /// </summary>
    [Fact]
    public async Task Reconcile_GlVoucherOfAnotherCompany_FailsWithGlVoucherNotFoundAndWritesNothing()
    {
        var transaction = Deposit(100m);
        var foreignVoucher = SeedGlVoucher(100m, companyId: Guid.NewGuid());
        var glBefore = GlSnapshot(_ledger);

        var result = await Handler().HandleAsync(
            Reconcile(_companyId, transaction.Id, new ReconciliationLine(null, foreignVoucher, 100m)),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BankingErrorCodes.GlVoucherNotFound, result.Error!.Code);
        Assert.Equal(BankTransactionStatus.Unreconciled, transaction.Status);
        Assert.Empty(_bank.Links);
        Assert.Equal(glBefore, GlSnapshot(_ledger));
    }

    /// <summary>
    /// Line amount != voucher total: typed 400 with zero writes. The $70 slice mismatches the
    /// $100 voucher total (the $30 voucher slice keeps the BN-04 line sum at $100).
    /// </summary>
    [Fact]
    public async Task Reconcile_GlLineAmountDiffersFromVoucherTotal_FailsWithGlAmountMismatch()
    {
        var transaction = Deposit(100m);
        var voucherId = SeedGlVoucher(100m);
        var glBefore = GlSnapshot(_ledger);

        var entry = Voucher(30m);
        var result = await Handler().HandleAsync(
            Reconcile(
                _companyId,
                transaction.Id,
                new ReconciliationLine(null, voucherId, 70m),
                PaySlice(entry.Id, 30m)),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BankingErrorCodes.GlAmountMismatch, result.Error!.Code);
        Assert.Equal(BankTransactionStatus.Unreconciled, transaction.Status);
        Assert.Equal(PaymentStatus.Unreconciled, entry.Status);
        Assert.Empty(_bank.Links);
        Assert.Equal(glBefore, GlSnapshot(_ledger));
    }

    // ------------------------------------------------------------ rejection paths

    [Fact]
    public async Task Reconcile_AlreadyReconciled_FailsWithInvalidStatusTransitionAndWritesNothing()
    {
        var transaction = Deposit(1000m, BankTransactionStatus.Reconciled);
        var entry = Voucher(1000m);

        var result = await Handler().HandleAsync(
            Reconcile(_companyId, transaction.Id, PaySlice(entry.Id, 1000m)),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BankingErrorCodes.InvalidStatusTransition, result.Error!.Code);
        Assert.Empty(_bank.Links);
        Assert.Empty(_bank.AddedGlEntries);
    }

    [Fact]
    public async Task Reconcile_ExcludedLine_FailsWithInvalidStatusTransition()
    {
        var transaction = Deposit(1000m, BankTransactionStatus.Excluded);
        var entry = Voucher(1000m);

        var result = await Handler().HandleAsync(
            Reconcile(_companyId, transaction.Id, PaySlice(entry.Id, 1000m)),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BankingErrorCodes.InvalidStatusTransition, result.Error!.Code);
        Assert.Empty(_bank.Links);
    }

    [Fact]
    public async Task Reconcile_UnknownTransaction_FailsWithBankTransactionNotFoundAndWritesNothing()
    {
        var entry = Voucher(1000m);

        var result = await Handler().HandleAsync(
            Reconcile(_companyId, Guid.NewGuid(), PaySlice(entry.Id, 1000m)),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BankingErrorCodes.BankTransactionNotFound, result.Error!.Code);
        Assert.Equal(PaymentStatus.Unreconciled, entry.Status);
        Assert.Empty(_bank.Links);
    }

    [Fact]
    public async Task Reconcile_UnknownPaymentEntry_FailsWithPaymentEntryNotFoundAndWritesNothing()
    {
        var transaction = Deposit(1000m);

        var result = await Handler().HandleAsync(
            Reconcile(_companyId, transaction.Id, PaySlice(Guid.NewGuid(), 1000m)),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BankingErrorCodes.PaymentEntryNotFound, result.Error!.Code);
        Assert.Equal(BankTransactionStatus.Unreconciled, transaction.Status);
        Assert.Empty(_bank.Links);
    }

    [Fact]
    public async Task Reconcile_TransactionOfAnotherCompany_FailsWithNotFoundWithoutMutatingIt()
    {
        // Company mismatch is reported as NOT FOUND: the id must not leak across companies.
        var foreignId = Guid.NewGuid();
        var foreign = Deposit(1000m);
        var entry = Voucher(1000m);

        var result = await Handler().HandleAsync(
            Reconcile(foreignId, foreign.Id, PaySlice(entry.Id, 1000m)),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BankingErrorCodes.BankTransactionNotFound, result.Error!.Code);
        Assert.Equal(BankTransactionStatus.Unreconciled, foreign.Status);
        Assert.Equal(0m, foreign.AllocatedAmount);
        Assert.Null(foreign.ClearanceDate);
        Assert.Equal(PaymentStatus.Unreconciled, entry.Status);
        Assert.Empty(_bank.Links);
        Assert.Empty(_bank.AddedGlEntries);
    }

    [Fact]
    public async Task Reconcile_PaymentEntryOfAnotherCompany_FailsWithPaymentEntryNotFound()
    {
        var transaction = Deposit(1000m);
        var foreign = Voucher(1000m);
        foreign.CompanyId = Guid.NewGuid();

        var result = await Handler().HandleAsync(
            Reconcile(_companyId, transaction.Id, PaySlice(foreign.Id, 1000m)),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BankingErrorCodes.PaymentEntryNotFound, result.Error!.Code);
        Assert.Equal(BankTransactionStatus.Unreconciled, transaction.Status);
        Assert.Empty(_bank.Links);
    }

    // ------------------------------------------------------------ over-consumption

    /// <summary>
    /// One $1,000 voucher consumed $600 by a first line: a second line asking $500 is
    /// rejected with the task 6.1 over-allocation code; the remaining $400 still reconciles.
    /// </summary>
    [Fact]
    public async Task Reconcile_OverConsumedVoucher_RejectsSecondLineButAllowsRemainder()
    {
        var entry = Voucher(1000m);
        var first = Deposit(600m);
        var ok = await Handler().HandleAsync(
            Reconcile(_companyId, first.Id, PaySlice(entry.Id, 600m)),
            CancellationToken.None);
        Assert.True(ok.IsSuccess);

        var greedy = Deposit(500m);
        var rejected = await Handler().HandleAsync(
            Reconcile(_companyId, greedy.Id, PaySlice(entry.Id, 500m)),
            CancellationToken.None);

        Assert.False(rejected.IsSuccess);
        Assert.Equal(BankingErrorCodes.OverAllocation, rejected.Error!.Code);
        Assert.Equal(BankTransactionStatus.Unreconciled, greedy.Status);
        Assert.Single(_bank.Links); // only the first line's link exists

        var remainder = Deposit(400m);
        var allowed = await Handler().HandleAsync(
            Reconcile(_companyId, remainder.Id, PaySlice(entry.Id, 400m)),
            CancellationToken.None);

        Assert.True(allowed.IsSuccess);
        Assert.Equal(2, _bank.Links.Count);
        Assert.Equal(1000m, _bank.Links.Sum(l => l.AllocatedAmount));
    }

    // ------------------------------------------------------------ line shape

    [Fact]
    public async Task Reconcile_LineWithBothCounterparts_FailsWithInvalidReconciliationAmount()
    {
        var transaction = Deposit(1000m);
        var entry = Voucher(1000m);

        var result = await Handler().HandleAsync(
            Reconcile(_companyId, transaction.Id, new ReconciliationLine(entry.Id, Guid.NewGuid(), 1000m)),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BankingErrorCodes.InvalidReconciliationAmount, result.Error!.Code);
        Assert.Empty(_bank.Links);
    }

    [Fact]
    public async Task Reconcile_LineWithNoCounterpart_FailsWithInvalidReconciliationAmount()
    {
        var transaction = Deposit(1000m);

        var result = await Handler().HandleAsync(
            Reconcile(_companyId, transaction.Id, new ReconciliationLine(null, null, 1000m)),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BankingErrorCodes.InvalidReconciliationAmount, result.Error!.Code);
        Assert.Empty(_bank.Links);
    }

    [Fact]
    public async Task Reconcile_NonPositiveLineAmount_FailsWithInvalidReconciliationAmount()
    {
        var transaction = Deposit(1000m);
        var entry = Voucher(1000m);

        var result = await Handler().HandleAsync(
            Reconcile(_companyId, transaction.Id, PaySlice(entry.Id, 0m)),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BankingErrorCodes.InvalidReconciliationAmount, result.Error!.Code);
        Assert.Empty(_bank.Links);
    }

    // ------------------------------------------------------------ BN-07 concurrency

    /// <summary>
    /// Scenario BN-07 (client token): the second clerk holds a stale RowVersion -&gt; the
    /// compare-and-swap pre-check fails fast with concurrency_conflict and zero writes.
    /// </summary>
    [Fact]
    public async Task Reconcile_StaleClientRowVersion_FailsWithConcurrencyConflictAndWritesNothing()
    {
        var transaction = Deposit(500m);
        var entry = Voucher(500m);

        var result = await Handler().HandleAsync(
            new ReconcileBankTransactionCommand(
                _companyId, transaction.Id, new[] { PaySlice(entry.Id, 500m) }, new byte[] { 0xEE, 0xFF }),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ConcurrencyErrorCodes.ConcurrencyConflict, result.Error!.Code);
        Assert.Equal(BankTransactionStatus.Unreconciled, transaction.Status);
        Assert.Equal(PaymentStatus.Unreconciled, entry.Status);
        Assert.Empty(_bank.Links);
        Assert.Empty(_bank.AddedGlEntries);
    }

    /// <summary>
    /// Scenario BN-07 (save race): the RowVersion WHERE clause matched 0 rows at SAVE time -
    /// the header save throws first, so no links are appended either.
    /// </summary>
    [Fact]
    public async Task Reconcile_RaceOnTransactionSave_FailsWithConcurrencyConflictAndNoLinks()
    {
        var transaction = Deposit(500m);
        var entry = Voucher(500m);
        _bank.FailNextTransactionUpdate = true;

        var result = await Handler().HandleAsync(
            Reconcile(_companyId, transaction.Id, PaySlice(entry.Id, 500m)),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ConcurrencyErrorCodes.ConcurrencyConflict, result.Error!.Code);
        Assert.Empty(_bank.Links);
        Assert.Empty(_bank.AddedGlEntries);
    }

    // ------------------------------------------------------------ BN-06 un-reconcile

    private UnreconcileBankTransactionCommandHandler UnreconcileHandler() => new(_bank);

    /// <summary>
    /// Scenario BN-06: un-reconcile deletes the links and reverts Status / AllocatedAmount /
    /// ClearanceDate on BOTH sides (clearance back to NULL, difference re-opened).
    /// </summary>
    [Fact]
    public async Task Unreconcile_ReconciledLine_RestoresNullsAndZeroAndReopensDifference()
    {
        var transaction = Deposit(1000m);
        var entry = Voucher(1000m);
        var reconciled = await Handler().HandleAsync(
            Reconcile(_companyId, transaction.Id, PaySlice(entry.Id, 1000m)),
            CancellationToken.None);
        Assert.True(reconciled.IsSuccess);
        Assert.Single(_bank.Links);

        var result = await UnreconcileHandler().HandleAsync(
            new UnreconcileBankTransactionCommand(_companyId, transaction.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);

        Assert.Equal(BankTransactionStatus.Unreconciled, transaction.Status);
        Assert.Equal(0m, transaction.AllocatedAmount);
        Assert.Null(transaction.ClearanceDate);

        Assert.Equal(PaymentStatus.Unreconciled, entry.Status);
        Assert.Null(entry.ClearanceDate);

        // The voucher's accounting date was never part of the cycle: still intact.
        Assert.Equal(PaymentDate, entry.PaymentDate);

        Assert.Empty(_bank.Links);

        // Difference re-opened: the full transaction amount is unallocated again.
        Assert.Equal(
            Math.Abs(transaction.Deposit - transaction.Withdrawal),
            Math.Abs(transaction.Deposit - transaction.Withdrawal) - transaction.AllocatedAmount);
        Assert.Empty(_bank.AddedGlEntries);
    }

    [Fact]
    public async Task Unreconcile_UnreconciledLine_FailsWithInvalidStatusTransition()
    {
        var transaction = Deposit(1000m);

        var result = await UnreconcileHandler().HandleAsync(
            new UnreconcileBankTransactionCommand(_companyId, transaction.Id),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BankingErrorCodes.InvalidStatusTransition, result.Error!.Code);
        Assert.Empty(_bank.Links);
    }

    [Fact]
    public async Task Unreconcile_UnknownTransaction_FailsWithBankTransactionNotFound()
    {
        var result = await UnreconcileHandler().HandleAsync(
            new UnreconcileBankTransactionCommand(_companyId, Guid.NewGuid()),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BankingErrorCodes.BankTransactionNotFound, result.Error!.Code);
    }

    [Fact]
    public async Task Unreconcile_TransactionOfAnotherCompany_FailsWithNotFoundAndKeepsLinks()
    {
        var transaction = Deposit(1000m);
        var entry = Voucher(1000m);
        Assert.True((await Handler().HandleAsync(
            Reconcile(_companyId, transaction.Id, PaySlice(entry.Id, 1000m)),
            CancellationToken.None)).IsSuccess);
        Assert.Single(_bank.Links);

        var result = await UnreconcileHandler().HandleAsync(
            new UnreconcileBankTransactionCommand(Guid.NewGuid(), transaction.Id),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BankingErrorCodes.BankTransactionNotFound, result.Error!.Code);
        Assert.Equal(BankTransactionStatus.Reconciled, transaction.Status);
        Assert.Single(_bank.Links);
    }

    [Fact]
    public async Task Unreconcile_StaleClientRowVersion_FailsWithConcurrencyConflictAndKeepsLinks()
    {
        var transaction = Deposit(1000m);
        var entry = Voucher(1000m);
        Assert.True((await Handler().HandleAsync(
            Reconcile(_companyId, transaction.Id, PaySlice(entry.Id, 1000m)),
            CancellationToken.None)).IsSuccess);

        var result = await UnreconcileHandler().HandleAsync(
            new UnreconcileBankTransactionCommand(_companyId, transaction.Id, new byte[] { 0xEE }),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ConcurrencyErrorCodes.ConcurrencyConflict, result.Error!.Code);
        Assert.Equal(BankTransactionStatus.Reconciled, transaction.Status);
        Assert.Single(_bank.Links);
    }
}
