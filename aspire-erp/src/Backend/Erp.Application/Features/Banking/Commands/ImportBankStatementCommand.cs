using Erp.Application.Common;

namespace Erp.Application.Features.Banking.Commands;

/// <summary>
/// Imports one bank statement file into isolated staging (task 6.2 / scenarios BN-01, BN-05):
/// the batch header plus one Unreconciled <c>BankTransaction</c> row per new line, inside ONE
/// transaction. Staging isolation (BN-01) holds BY CONSTRUCTION: the handler depends on no GL
/// or ledger writer, so zero <c>GLEntry</c> rows can be posted.
/// </summary>
/// <param name="CompanyId">Company that owns the bank account.</param>
/// <param name="BankAccountId">Target bank account of the import.</param>
/// <param name="FileName">Uploaded file name recorded on the batch header.</param>
/// <param name="RawContent">Raw file content (CSV or OFX 1.x SGML).</param>
/// <param name="Format">Statement format: "CSV" or "OFX" (case-insensitive).</param>
public sealed record ImportBankStatementCommand(
    Guid CompanyId,
    Guid BankAccountId,
    string FileName,
    string RawContent,
    string Format) : ICommand<Result<BankStatementImportSummary>>;

/// <summary>
/// Import outcome (scenario BN-05): total rows parsed vs new rows persisted vs FITID duplicates
/// skipped - both for already-imported ids and for ids duplicated inside the batch.
/// </summary>
/// <param name="ImportId">Persisted batch header id.</param>
/// <param name="FileName">Uploaded file name.</param>
/// <param name="TotalTransactions">Raw rows parsed from the file.</param>
/// <param name="ImportedCount">Rows persisted as staging transactions.</param>
/// <param name="DuplicateCount">Rows skipped as duplicates.</param>
public sealed record BankStatementImportSummary(
    Guid ImportId,
    string FileName,
    int TotalTransactions,
    int ImportedCount,
    int DuplicateCount);
