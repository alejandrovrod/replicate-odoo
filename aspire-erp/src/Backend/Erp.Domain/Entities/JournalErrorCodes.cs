namespace Erp.Domain.Entities;

/// <summary>
/// Stable machine-readable failure codes owned by the Journal Entry pipeline (tasks.md 2.3/2.4).
/// They flow Domain -&gt; Application (<c>Error.Code</c>) -&gt; Api, where JournalEntriesController
/// maps them to RFC 7807 status codes (workflow/lock/concurrency conflicts -&gt; 409, everything
/// else -&gt; 400), mirroring <see cref="StockErrorCodes"/>/<see cref="PurchaseErrorCodes"/>.
/// </summary>
/// <remarks>
/// Two codes of this pipeline are deliberately NOT declared here because they already exist in the
/// shared vocabulary and "one concept, one wire value" wins over module ownership:
/// <list type="bullet">
/// <item>imbalance = <see cref="StockErrorCodes.DoubleEntryImbalance"/> (Constitution III.1, the
/// same rule every posting pipeline enforces);</item>
/// <item>freeze = <see cref="AccountingErrorCodes.FiscalPeriodLocked"/> and group account =
/// <see cref="AccountingErrorCodes.PostingToGroupAccountProhibited"/> (both accounting-wide rules,
/// see the remarks on <see cref="AccountingErrorCodes"/>).</item>
/// </list>
/// </remarks>
public static class JournalErrorCodes
{
    /// <summary>No JournalEntry with that id exists in this tenant/company (mapped to 404 by the API).</summary>
    public const string JournalEntryNotFound = "journal_entry_not_found";

    /// <summary>
    /// The requested state change is illegal for the voucher's current status (cancel a Draft,
    /// submit twice, ...). Same wire value as the buying workflow - one language for one concept;
    /// mapped to HTTP 409.
    /// </summary>
    public const string InvalidStatusTransition = "invalid_status_transition";

    /// <summary>Company lookup failed (shared wire value used by every module).</summary>
    public const string CompanyNotFound = "company_not_found";

    /// <summary>A line references an Account id that does not exist in this tenant.</summary>
    public const string AccountNotFound = "account_not_found";

    /// <summary>
    /// The referenced account is inactive or belongs to another company, so it may not receive
    /// ledger rows (same wire value as <see cref="PurchaseErrorCodes.InvalidGlAccount"/>,
    /// which the buying engine raises for the identical rule).
    /// </summary>
    public const string InvalidGlAccount = "invalid_gl_account";

    /// <summary>The voucher carries no lines at all (shared wire value with the other documents).</summary>
    public const string NoLines = "no_lines";

    /// <summary>
    /// A line's amounts violate decimal(18,4) money rules: negative, or zero on BOTH sides
    /// (Constitution IV.3 requires Debit &gt;= 0 and Credit &gt;= 0; a line that posts nothing is
    /// a client error).
    /// </summary>
    public const string InvalidAmount = "invalid_amount";
}
