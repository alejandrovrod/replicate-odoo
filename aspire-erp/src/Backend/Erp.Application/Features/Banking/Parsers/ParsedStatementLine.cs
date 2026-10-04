namespace Erp.Application.Features.Banking.Parsers;

/// <summary>
/// One parsed statement line, format-agnostic. The import handler validates it (BN-02),
/// de-duplicates it by <see cref="TransactionId"/> (BN-05) and persists it as an Unreconciled
/// <c>BankTransaction</c> staging row.
/// </summary>
/// <param name="LineNumber">1-based line (CSV) or block (OFX) number in the source file, for errors.</param>
/// <param name="TransactionDate">Value date of the line.</param>
/// <param name="Deposit">Money in (&gt;= 0).</param>
/// <param name="Withdrawal">Money out (&gt;= 0).</param>
/// <param name="Description">Statement narrative.</param>
/// <param name="ReferenceNumber">Bank reference, when the format carries one.</param>
/// <param name="TransactionId">Stable bank id (OFX FITID) for de-duplication; null when absent.</param>
public sealed record ParsedStatementLine(
    int LineNumber,
    DateOnly TransactionDate,
    decimal Deposit,
    decimal Withdrawal,
    string Description,
    string? ReferenceNumber,
    string? TransactionId);
