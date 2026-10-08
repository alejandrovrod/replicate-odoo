namespace Erp.Domain.Entities;

/// <summary>
/// Stable machine-readable failure codes owned by the Accounting domain for the Fiscal Closing
/// module (R-13, spec §5). They flow Domain -&gt; Application (<c>Error.Code</c>) -&gt; Api,
/// where the controllers map them to RFC 7807 status codes (plan.md §5).
/// </summary>
/// <remarks>
/// Supersedes the <c>BankingErrorCodes.period_closing_*</c> constants, which were owned by the
/// wrong domain (spec §7 audit). All values are snake_case per the module convention.
/// </remarks>
public static class FiscalClosingErrorCodes
{
    /// <summary>Target fiscal year <c>IsClosed = true</c> (HTTP 409).</summary>
    public const string FiscalYearClosed = "fiscal_year_closed";

    /// <summary>New year overlaps an existing year of the same company (HTTP 409).</summary>
    public const string FiscalYearOverlap = "fiscal_year_overlap";

    /// <summary>Fiscal year id unknown (HTTP 404).</summary>
    public const string FiscalYearNotFound = "fiscal_year_not_found";

    /// <summary><c>PostingDate ∉ [StartDate, EndDate]</c> of the fiscal year (HTTP 422).</summary>
    public const string ClosingDateOutsideFiscalYear = "closing_date_outside_fiscal_year";

    /// <summary>
    /// Retained leaf missing / group / inactive / wrong RootType / wrong company-tenant
    /// (HTTP 400).
    /// </summary>
    public const string InvalidRetainedEarningsAccount = "invalid_retained_earnings_account";

    /// <summary>
    /// Engine attempted to close a non-P&amp;L account — defense-in-depth tripwire signalling an
    /// internal bug, not caller error (HTTP 500).
    /// </summary>
    public const string ClosingNonPLAccount = "closing_non_pl_account";

    /// <summary>
    /// A submitted non-cancelled voucher already exists for <c>(CompanyId, FiscalYearId)</c>
    /// (HTTP 409).
    /// </summary>
    public const string DuplicateClosingForFiscalYear = "duplicate_closing_for_fiscal_year";

    /// <summary>Zero P&amp;L balances in the year window (HTTP 422).</summary>
    public const string NoClosingBalances = "no_closing_balances";

    /// <summary>
    /// Computed close does not net to zero — internal tripwire (HTTP 500).
    /// Same wire value as <c>StockErrorCodes.DoubleEntryImbalance</c> (one vocabulary, two owners).
    /// </summary>
    public const string DoubleEntryImbalance = "double_entry_imbalance";

    /// <summary>Closing voucher id unknown (HTTP 404).</summary>
    public const string PeriodClosingNotFound = "period_closing_not_found";

    /// <summary>Illegal <c>Draft → Submitted → Cancelled</c> step (HTTP 409).</summary>
    public const string PeriodClosingInvalidTransition = "period_closing_invalid_transition";

    /// <summary>Double cancel of a <c>Cancelled</c> voucher (HTTP 409).</summary>
    public const string PeriodClosingAlreadyCancelled = "period_closing_already_cancelled";

    /// <summary>Stale <c>RowVersion</c> (HTTP 409).</summary>
    public const string ConcurrencyConflict = "concurrency_conflict";
}
