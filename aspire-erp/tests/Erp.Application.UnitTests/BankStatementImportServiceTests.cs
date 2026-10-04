using System.Text;
using Erp.Application.Features.Banking.Commands;
using Erp.Application.Features.Banking.Parsers;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Task 6.2 acceptance through the CQRS handler against in-memory doubles: CSV import lands
/// staging rows with zero GL writes (BN-01), OFX FITIDs map directions correctly, re-imports
/// skip duplicates with exact counts (BN-05), BN-02 violations and unknown accounts fail with
/// typed codes and ZERO persisted rows.
/// </summary>
public sealed class BankStatementImportServiceTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _accountId = Guid.NewGuid();
    private readonly Guid _glAccountId = Guid.NewGuid();
    private readonly FakeBankRepository _bank = new();

    public BankStatementImportServiceTests()
    {
        _bank.SeedAccount(new BankAccount
        {
            Id = _accountId,
            TenantId = _tenantId,
            CompanyId = _companyId,
            AccountName = "Main Checking",
            BankName = "Acme Bank",
            AccountNumber = "0012345678",
            GLAccountId = _glAccountId,
        });
    }

    private ImportBankStatementCommandHandler Handler() =>
        new(_bank, new CsvStatementParser(), new OfxStatementParser());

    private static string CsvWith(int lines, Func<int, string>? row = null)
    {
        var sb = new StringBuilder("Date,Description,Amount\n");
        for (var i = 1; i <= lines; i++)
        {
            sb.Append(row is null
                ? $"2026-09-{(i % 28) + 1:00},STRIPE PAYOUT REF #{90000 + i},{100 + i:0.00}\n"
                : row(i) + "\n");
        }

        return sb.ToString();
    }

    private static string OfxWith(params (string FitId, string Amount, string Name)[] rows)
    {
        var sb = new StringBuilder("OFXHEADER:100\n<OFX><BANKMSGSRSV1><STMTTRNRS><STMTRS>\n");
        foreach (var (fitId, amount, name) in rows)
        {
            sb.Append("<STMTTRN>\n")
                .Append("<TRNTYPE>CREDIT\n")
                .Append($"<DTPOSTED>20261002\n")
                .Append($"<TRNAMT>{amount}\n")
                .Append($"<FITID>{fitId}\n")
                .Append($"<NAME>{name}\n")
                .Append("</STMTTRN>\n");
        }

        return sb.Append("</STMTRS></STMTTRNRS></BANKMSGSRSV1></OFX>").ToString();
    }

    // ------------------------------------------------------------ BN-01 staging isolation

    /// <summary>
    /// Scenario BN-01: 50 CSV lines -&gt; 50 Unreconciled staging rows and ZERO GLEntry writes.
    /// </summary>
    [Fact]
    public async Task Import_CsvWith50Lines_Persists50UnreconciledRowsAndZeroGlWrites()
    {
        var result = await Handler().HandleAsync(
            new ImportBankStatementCommand(_companyId, _accountId, "stmt.csv", CsvWith(50), "CSV"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var summary = result.Value!;
        Assert.Equal(50, summary.TotalTransactions);
        Assert.Equal(50, summary.ImportedCount);
        Assert.Equal(0, summary.DuplicateCount);
        Assert.Equal(1, _bank.TransactionCount); // ONE transaction

        Assert.Equal(50, _bank.AddedTransactions.Count);
        Assert.All(_bank.AddedTransactions, t =>
        {
            Assert.Equal(BankTransactionStatus.Unreconciled, t.Status);
            Assert.Equal(_accountId, t.BankAccountId);
            Assert.Equal(_companyId, t.CompanyId);
            Assert.Equal(_tenantId, t.TenantId);
        });
        Assert.Single(_bank.PersistedImports);

        // Staging isolation (BN-01): the import posts nothing to the General Ledger.
        Assert.Empty(_bank.AddedGlEntries);
    }

    // ------------------------------------------------------------ OFX directions + BN-05

    /// <summary>
    /// OFX FITIDs map to TransactionId; the TRNAMT sign decides deposit vs withdrawal.
    /// </summary>
    [Fact]
    public async Task Import_OfxWithFitIds_MapsStatusesAmountsAndDirections()
    {
        var result = await Handler().HandleAsync(
            new ImportBankStatementCommand(
                _companyId, _accountId, "stmt.ofx",
                OfxWith(("FIT-1", "5400.00", "STRIPE PAYOUT"), ("FIT-2", "-15.00", "BANK FEE")),
                "OFX"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.ImportedCount);

        var payout = Assert.Single(_bank.AddedTransactions, t => t.TransactionId == "FIT-1");
        Assert.Equal(5400m, payout.Deposit);
        Assert.Equal(0m, payout.Withdrawal);
        Assert.Equal("STRIPE PAYOUT", payout.Description);
        Assert.Equal(new DateOnly(2026, 10, 2), payout.TransactionDate);

        var fee = Assert.Single(_bank.AddedTransactions, t => t.TransactionId == "FIT-2");
        Assert.Equal(0m, fee.Deposit);
        Assert.Equal(15m, fee.Withdrawal);
    }

    /// <summary>
    /// Scenario BN-05: re-importing the same file imports nothing and reports exact counts.
    /// </summary>
    [Fact]
    public async Task Import_SameOfxFileTwice_SecondImportSkipsAllDuplicates()
    {
        var handler = Handler();
        var raw = OfxWith(("FIT-1", "100.00", "A"), ("FIT-2", "200.00", "B"), ("FIT-3", "-50.00", "C"));

        var first = await handler.HandleAsync(
            new ImportBankStatementCommand(_companyId, _accountId, "stmt.ofx", raw, "ofx"),
            CancellationToken.None);
        Assert.True(first.IsSuccess);
        Assert.Equal(3, first.Value!.ImportedCount);
        Assert.Equal(0, first.Value.DuplicateCount);

        var second = await handler.HandleAsync(
            new ImportBankStatementCommand(_companyId, _accountId, "stmt.ofx", raw, "OFX"),
            CancellationToken.None);
        Assert.True(second.IsSuccess);
        Assert.Equal(3, second.Value!.TotalTransactions);
        Assert.Equal(0, second.Value.ImportedCount);
        Assert.Equal(3, second.Value.DuplicateCount);

        // No new staging rows from the re-import (3 total), still zero GL writes.
        Assert.Equal(3, _bank.PersistedTransactions.Count);
        Assert.Empty(_bank.AddedGlEntries);
    }

    /// <summary>FITIDs duplicated WITHIN one batch: the first wins, the rest are duplicates.</summary>
    [Fact]
    public async Task Import_DuplicateFitIdWithinBatch_ImportsFirstSkipsRest()
    {
        var result = await Handler().HandleAsync(
            new ImportBankStatementCommand(
                _companyId, _accountId, "stmt.ofx",
                OfxWith(("FIT-1", "100.00", "A"), ("FIT-1", "100.00", "A again")),
                "OFX"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.TotalTransactions);
        Assert.Equal(1, result.Value.ImportedCount);
        Assert.Equal(1, result.Value.DuplicateCount);
        Assert.Single(_bank.AddedTransactions);
    }

    // ------------------------------------------------------------ rejection paths (zero writes)

    /// <summary>BN-02: debit AND credit on one line is rejected with zero rows persisted.</summary>
    [Fact]
    public async Task Import_CsvWithBothSidesPositive_FailsWithBothSidesPostedAndWritesNothing()
    {
        var raw = "Date,Description,Debit,Credit\n2026-10-02,AMBIGUOUS,100.00,50.00\n";

        var before = _bank.PersistedTransactions
            .Select(t => (t.TransactionId, t.Deposit, t.Withdrawal))
            .ToList();

        var result = await Handler().HandleAsync(
            new ImportBankStatementCommand(_companyId, _accountId, "bad.csv", raw, "CSV"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BankingErrorCodes.BothSidesPosted, result.Error!.Code);
        Assert.Empty(_bank.PersistedImports);
        Assert.Empty(_bank.AddedTransactions);
        Assert.Equal(before, _bank.PersistedTransactions
            .Select(t => (t.TransactionId, t.Deposit, t.Withdrawal)));
        Assert.Empty(_bank.AddedGlEntries);
    }

    /// <summary>BN-02: a negative side is rejected with zero rows persisted.</summary>
    [Fact]
    public async Task Import_CsvWithNegativeSide_FailsWithNegativeAmountAndWritesNothing()
    {
        var raw = "Date,Description,Debit,Credit\n2026-10-02,REFUND,-5.00,0.00\n";

        var result = await Handler().HandleAsync(
            new ImportBankStatementCommand(_companyId, _accountId, "bad.csv", raw, "CSV"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BankingErrorCodes.NegativeTransactionAmount, result.Error!.Code);
        Assert.Empty(_bank.PersistedImports);
        Assert.Empty(_bank.AddedTransactions);
        Assert.Empty(_bank.AddedGlEntries);
    }

    /// <summary>Unknown account: typed failure, zero writes (the id must not leak rows).</summary>
    [Fact]
    public async Task Import_UnknownAccount_FailsWithBankAccountNotFoundAndWritesNothing()
    {
        var result = await Handler().HandleAsync(
            new ImportBankStatementCommand(_companyId, Guid.NewGuid(), "stmt.csv", CsvWith(3), "CSV"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BankingErrorCodes.BankAccountNotFound, result.Error!.Code);
        Assert.Empty(_bank.PersistedImports);
        Assert.Empty(_bank.AddedTransactions);
        Assert.Empty(_bank.AddedGlEntries);
    }

    /// <summary>Account of another company: reported as NOT FOUND, zero writes.</summary>
    [Fact]
    public async Task Import_AccountOfAnotherCompany_FailsWithBankAccountNotFoundAndWritesNothing()
    {
        var result = await Handler().HandleAsync(
            new ImportBankStatementCommand(Guid.NewGuid(), _accountId, "stmt.csv", CsvWith(3), "CSV"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BankingErrorCodes.BankAccountNotFound, result.Error!.Code);
        Assert.Empty(_bank.PersistedImports);
        Assert.Empty(_bank.AddedTransactions);
    }

    /// <summary>Unsupported format: typed failure before any parsing or persistence.</summary>
    [Fact]
    public async Task Import_UnsupportedFormat_FailsWithInvalidStatementFormatAndWritesNothing()
    {
        var result = await Handler().HandleAsync(
            new ImportBankStatementCommand(_companyId, _accountId, "stmt.qif", "anything", "QIF"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BankingErrorCodes.InvalidStatementFormat, result.Error!.Code);
        Assert.Empty(_bank.PersistedImports);
        Assert.Empty(_bank.AddedTransactions);
    }
}
