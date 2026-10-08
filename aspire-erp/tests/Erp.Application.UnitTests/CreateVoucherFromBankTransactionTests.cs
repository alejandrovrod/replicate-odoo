using Erp.Application.Features.Banking.Commands;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Task 6.5 acceptance through the CQRS handler against in-memory doubles: scenario BN-04
/// (a $15 bank fee becomes Dr 5150 / Cr bank with the staging line Reconciled, atomically),
/// every rejection with zero writes, and the BN-07 stale-token conflict.
/// </summary>
public sealed class CreateVoucherFromBankTransactionTests
{
    private static readonly DateOnly StatementDate = new(2026, 10, 2);

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _bankAccountId = Guid.NewGuid();
    private readonly Guid _bankGlAccountId = Guid.NewGuid();
    private readonly Guid _expenseAccountId = Guid.NewGuid();

    private readonly FakeBankRepository _bank = new();
    private readonly FakeAccountRepository _accounts = new();
    private readonly FakeCompanyRepository _companies = new();
    private readonly FakeJournalRepository _journals = new();

    public CreateVoucherFromBankTransactionTests()
    {
        _bank.SeedAccount(new BankAccount
        {
            Id = _bankAccountId,
            TenantId = _tenantId,
            CompanyId = _companyId,
            AccountName = "Main Checking",
            BankName = "Acme Bank",
            AccountNumber = "0012345678",
            GLAccountId = _bankGlAccountId,
        });

        var bankGl = new Account
        {
            Id = _bankGlAccountId,
            TenantId = _tenantId,
            CompanyId = _companyId,
            AccountCode = "1110",
            AccountName = "Cash and Cash Equivalents",
            RootType = AccountRootType.Asset,
            IsGroup = false,
            IsActive = true,
        };
        var expense = new Account
        {
            Id = _expenseAccountId,
            TenantId = _tenantId,
            CompanyId = _companyId,
            AccountCode = "5150",
            AccountName = "Bank Fees",
            RootType = AccountRootType.Expense,
            IsGroup = false,
            IsActive = true,
        };
        _accounts.Seed(bankGl, expense);
        _accounts.AccountsByCodeMap["5150"] = new[] { expense };

        _companies.Company = new Company
        {
            Id = _companyId,
            TenantId = _tenantId,
            Name = "Acme",
            TaxId = "TAX",
        };
    }

    private BankTransaction Withdrawal(decimal amount, BankTransactionStatus status = BankTransactionStatus.Unreconciled)
    {
        var transaction = new BankTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            CompanyId = _companyId,
            BankAccountId = _bankAccountId,
            TransactionDate = StatementDate,
            Deposit = 0m,
            Withdrawal = amount,
            Description = "MONTHLY BANK FEE",
            Status = status,
            RowVersion = new byte[] { 0x01 },
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _bank.SeedTransaction(transaction);
        return transaction;
    }

    private CreateVoucherFromBankTransactionCommandHandler Handler() =>
        new(_bank, _accounts, _companies, _journals);

    /// <summary>
    /// Scenario BN-04 exact: a $15 withdrawal with expense code 5150 posts Dr 5150 / Cr 1110 $15
    /// as a SUBMITTED voucher and reconciles the line in ONE transaction.
    /// </summary>
    [Fact]
    public async Task QuickVoucher_FeeWithdrawal_PostsBalancedSubmittedVoucherAndReconcilesAtomically()
    {
        var transaction = Withdrawal(15m);

        var result = await Handler().HandleAsync(
            new CreateVoucherFromBankTransactionCommand(_companyId, transaction.Id, "5150"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var entry = result.Value!;
        Assert.Equal(JournalEntryStatus.Submitted, entry.Status);
        Assert.StartsWith("JV-", entry.VoucherNo, StringComparison.Ordinal);
        Assert.Equal(StatementDate, entry.PostingDate);

        // Dr expense / Cr bank, exactly $15 each side.
        Assert.Equal(2, entry.Lines.Count);
        var debit = entry.Lines.Single(l => l.Debit > 0m);
        var credit = entry.Lines.Single(l => l.Credit > 0m);
        Assert.Equal("5150", debit.AccountCode);
        Assert.Equal(15m, debit.Debit);
        Assert.Equal("1110", credit.AccountCode);
        Assert.Equal(15m, credit.Credit);

        // The persisted ledger rows are the same balanced pair (append-only, two rows).
        Assert.Equal(2, _journals.GlEntries.Count);
        Assert.Equal(15m, _journals.GlEntries.Sum(l => l.Debit));
        Assert.Equal(15m, _journals.GlEntries.Sum(l => l.Credit));

        // Reconcile tail mirrors the 6.4 link semantics: one GLEntry slice for the voucher.
        Assert.Equal(BankTransactionStatus.Reconciled, transaction.Status);
        Assert.Equal(15m, transaction.AllocatedAmount);
        Assert.Equal(StatementDate, transaction.ClearanceDate);
        var link = Assert.Single(_bank.Links);
        Assert.Equal("GLEntry", link.CounterpartType.ToString());
        Assert.Equal(entry.Id, link.CounterpartId);
        Assert.Equal(15m, link.AllocatedAmount);

        // Difference is $0.00 and everything ran inside ONE transaction.
        Assert.Equal(0m, Math.Abs(transaction.Deposit - transaction.Withdrawal) - transaction.AllocatedAmount);
        Assert.Equal(1, _bank.TransactionCount);
    }

    /// <summary>An explicit amount equal to |Deposit - Withdrawal| posts that amount.</summary>
    [Fact]
    public async Task QuickVoucher_ExplicitMatchingAmount_PostsIt()
    {
        var transaction = Withdrawal(15m);

        var result = await Handler().HandleAsync(
            new CreateVoucherFromBankTransactionCommand(_companyId, transaction.Id, "5150", 15m, "Bank fee Oct"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Bank fee Oct", result.Value!.UserRemark);
        Assert.Equal(BankTransactionStatus.Reconciled, transaction.Status);
    }

    /// <summary>An already-Reconciled line is a 409 with zero writes (no voucher, no rows, no links).</summary>
    [Fact]
    public async Task QuickVoucher_AlreadyReconciled_FailsWithInvalidStatusTransitionAndWritesNothing()
    {
        var transaction = Withdrawal(15m, BankTransactionStatus.Reconciled);

        var result = await Handler().HandleAsync(
            new CreateVoucherFromBankTransactionCommand(_companyId, transaction.Id, "5150"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BankingErrorCodes.InvalidStatusTransition, result.Error!.Code);
        Assert.Empty(_journals.Entries);
        Assert.Empty(_journals.GlEntries);
        Assert.Empty(_bank.Links);
    }

    /// <summary>An unknown expense code is a 400 with zero writes.</summary>
    [Fact]
    public async Task QuickVoucher_UnknownExpenseCode_FailsWithExpenseAccountNotFoundAndWritesNothing()
    {
        var transaction = Withdrawal(15m);

        var result = await Handler().HandleAsync(
            new CreateVoucherFromBankTransactionCommand(_companyId, transaction.Id, "9999"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BankingErrorCodes.ExpenseAccountNotFound, result.Error!.Code);
        Assert.Equal(BankTransactionStatus.Unreconciled, transaction.Status);
        Assert.Empty(_journals.Entries);
        Assert.Empty(_journals.GlEntries);
        Assert.Empty(_bank.Links);
    }

    /// <summary>An explicit amount off by one cent is a 400 with zero writes (BN-04 exactness).</summary>
    [Fact]
    public async Task QuickVoucher_AmountMismatch_FailsWithReconciliationAmountMismatchAndWritesNothing()
    {
        var transaction = Withdrawal(15m);

        var result = await Handler().HandleAsync(
            new CreateVoucherFromBankTransactionCommand(_companyId, transaction.Id, "5150", 14.99m),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BankingErrorCodes.ReconciliationAmountMismatch, result.Error!.Code);
        Assert.Equal(BankTransactionStatus.Unreconciled, transaction.Status);
        Assert.Empty(_journals.Entries);
        Assert.Empty(_journals.GlEntries);
        Assert.Empty(_bank.Links);
    }

    /// <summary>
    /// BN-07 (client token): a stale RowVersion fails fast with concurrency_conflict and zero writes.
    /// </summary>
    [Fact]
    public async Task QuickVoucher_StaleClientRowVersion_FailsWithConcurrencyConflictAndWritesNothing()
    {
        var transaction = Withdrawal(15m);

        var result = await Handler().HandleAsync(
            new CreateVoucherFromBankTransactionCommand(
                _companyId, transaction.Id, "5150", null, null, new byte[] { 0xEE, 0xFF }),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ConcurrencyErrorCodes.ConcurrencyConflict, result.Error!.Code);
        Assert.Equal(BankTransactionStatus.Unreconciled, transaction.Status);
        Assert.Empty(_journals.Entries);
        Assert.Empty(_journals.GlEntries);
        Assert.Empty(_bank.Links);
    }

    /// <summary>
    /// BN-07 (save race): the RowVersion WHERE clause matched 0 rows at SAVE time - the header
    /// save throws first, so no reconciliation link is appended either.
    /// </summary>
    [Fact]
    public async Task QuickVoucher_RaceOnTransactionSave_FailsWithConcurrencyConflictAndNoLinks()
    {
        var transaction = Withdrawal(15m);
        _bank.FailNextTransactionUpdate = true;

        var result = await Handler().HandleAsync(
            new CreateVoucherFromBankTransactionCommand(_companyId, transaction.Id, "5150"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ConcurrencyErrorCodes.ConcurrencyConflict, result.Error!.Code);
        Assert.Empty(_bank.Links);
    }
}
