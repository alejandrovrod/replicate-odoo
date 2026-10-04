using Erp.Application.Features.Banking.Parsers;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Parser unit tests (task 6.2): the CSV sign convention and debit/credit mapping, the OFX
/// SGML mapping, and malformed inputs (bad dates, bad amounts, truncated OFX) rejected with
/// clear error codes and line numbers.
/// </summary>
public sealed class StatementParserTests
{
    private readonly CsvStatementParser _csv = new();
    private readonly OfxStatementParser _ofx = new();

    // ------------------------------------------------------------------ CSV happy paths

    [Fact]
    public void Csv_NegativeAmount_MapsToWithdrawal()
    {
        var rows = _csv.Parse("Date,Description,Amount\n2026-10-02,BANK FEE,-15.00\n");

        var row = Assert.Single(rows);
        Assert.Equal(0m, row.Deposit);
        Assert.Equal(15m, row.Withdrawal);
        Assert.Equal("BANK FEE", row.Description);
        Assert.Equal(new DateOnly(2026, 10, 2), row.TransactionDate);
    }

    [Fact]
    public void Csv_DebitCreditColumns_MapToWithdrawalAndDeposit()
    {
        var rows = _csv.Parse(
            "Transaction Date,Memo,Debit,Credit\n"
            + "2026-10-02,SUPPLIER PAYMENT,250.00,0.00\n"
            + "2026-10-03,CUSTOMER RECEIPT,0.00,999.50\n");

        Assert.Equal(2, rows.Count);
        Assert.Equal(250m, rows[0].Withdrawal);
        Assert.Equal(0m, rows[0].Deposit);
        Assert.Equal(999.50m, rows[1].Deposit);
        Assert.Equal(0m, rows[1].Withdrawal);
    }

    [Fact]
    public void Csv_QuotedDescriptionWithComma_ParsesAsOneField()
    {
        var rows = _csv.Parse("Date,Description,Amount\n2026-10-02,\"ACME, INC. PAYOUT\",10.00\n");

        Assert.Equal("ACME, INC. PAYOUT", Assert.Single(rows).Description);
    }

    // ------------------------------------------------------------------ CSV malformed

    [Fact]
    public void Csv_BadDate_ThrowsMalformedRowWithLineNumber()
    {
        var ex = Assert.Throws<BankingValidationException>(() =>
            _csv.Parse("Date,Description,Amount\n2026-10-02,OK,10.00\nnot-a-date,BROKEN,5.00\n"));

        Assert.Equal(BankingErrorCodes.MalformedCsvRow, ex.Code);
        Assert.Contains("Line 3", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Csv_BadAmount_ThrowsMalformedRowWithLineNumber()
    {
        var ex = Assert.Throws<BankingValidationException>(() =>
            _csv.Parse("Date,Description,Amount\n2026-10-02,BROKEN,ten\n"));

        Assert.Equal(BankingErrorCodes.MalformedCsvRow, ex.Code);
        Assert.Contains("Line 2", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Csv_MissingAmountAndCreditColumns_ThrowsMalformedRow()
    {
        var ex = Assert.Throws<BankingValidationException>(() =>
            _csv.Parse("Date,Description\n2026-10-02,NO MONEY\n"));

        Assert.Equal(BankingErrorCodes.MalformedCsvRow, ex.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Date,Description,Amount\n")]
    public void Csv_EmptyContent_ThrowsEmptyStatement(string raw)
    {
        var ex = Assert.Throws<BankingValidationException>(() => _csv.Parse(raw));

        Assert.Equal(BankingErrorCodes.EmptyStatement, ex.Code);
    }

    // ------------------------------------------------------------------ OFX happy paths

    [Fact]
    public void Ofx_NameAndMemo_CombineIntoDescriptionWithFitId()
    {
        var rows = _ofx.Parse(
            "<OFX>\n<STMTTRN>\n<TRNTYPE>CREDIT\n<DTPOSTED>20261002120000\n<TRNAMT>5400.00\n"
            + "<FITID>2026100201\n<NAME>STRIPE PAYOUT\n<MEMO>REF #98234\n</STMTTRN>\n");

        var row = Assert.Single(rows);
        Assert.Equal("2026100201", row.TransactionId);
        Assert.Equal(5400m, row.Deposit);
        Assert.Equal(0m, row.Withdrawal);
        Assert.Equal("STRIPE PAYOUT REF #98234", row.Description);
        Assert.Equal(new DateOnly(2026, 10, 2), row.TransactionDate);
    }

    // ------------------------------------------------------------------ OFX malformed

    [Fact]
    public void Ofx_MissingAmount_ThrowsMalformedBlockWithLineNumber()
    {
        var ex = Assert.Throws<BankingValidationException>(() =>
            _ofx.Parse("<OFX>\n<STMTTRN>\n<TRNTYPE>DEBIT\n<DTPOSTED>20261002\n<FITID>1\n</STMTTRN>\n"));

        Assert.Equal(BankingErrorCodes.MalformedOfxBlock, ex.Code);
        Assert.Contains("line 2", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Ofx_BadPostedDate_ThrowsMalformedBlockWithLineNumber()
    {
        var ex = Assert.Throws<BankingValidationException>(() =>
            _ofx.Parse("<OFX>\n<STMTTRN>\n<TRNAMT>-15.00\n<DTPOSTED>tomorrow\n</STMTTRN>\n"));

        Assert.Equal(BankingErrorCodes.MalformedOfxBlock, ex.Code);
        Assert.Contains("line 2", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Ofx_UnterminatedBlock_ThrowsMalformedBlockWithLineNumber()
    {
        var ex = Assert.Throws<BankingValidationException>(() =>
            _ofx.Parse("<OFX>\n<STMTTRN>\n<TRNAMT>10.00\n<DTPOSTED>20261002\n"));

        Assert.Equal(BankingErrorCodes.MalformedOfxBlock, ex.Code);
        Assert.Contains("line 2", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("<OFX><BANKMSGSRSV1></BANKMSGSRSV1></OFX>")]
    public void Ofx_EmptyContent_ThrowsEmptyStatement(string raw)
    {
        var ex = Assert.Throws<BankingValidationException>(() => _ofx.Parse(raw));

        Assert.Equal(BankingErrorCodes.EmptyStatement, ex.Code);
    }

    // ------------------------------------------------------------------ BN-02 validator

    [Fact]
    public void Validator_BothSidesPositive_ThrowsBothSidesPosted()
    {
        var ex = Assert.Throws<BankingValidationException>(
            () => BankTransactionValidator.EnsureValidSides(100m, 50m));

        Assert.Equal(BankingErrorCodes.BothSidesPosted, ex.Code);
    }

    [Fact]
    public void Validator_NegativeSide_ThrowsNegativeTransactionAmount()
    {
        var ex = Assert.Throws<BankingValidationException>(
            () => BankTransactionValidator.EnsureValidSides(-1m, 0m));

        Assert.Equal(BankingErrorCodes.NegativeTransactionAmount, ex.Code);
    }
}
