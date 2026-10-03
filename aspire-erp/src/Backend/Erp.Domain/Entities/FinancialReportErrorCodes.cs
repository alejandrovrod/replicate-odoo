namespace Erp.Domain.Entities;

/// <summary>
/// Stable machine-readable failure codes of the Financial Reporting read endpoints
/// (tasks.md 2.5). They flow Domain -&gt; Api, where <c>FinancialReportsController</c> maps them to
/// RFC 7807 ProblemDetails with a <c>code</c> extension - the same contract every other
/// controller follows (mirrors <see cref="AccountErrorCodes"/>/<see cref="JournalErrorCodes"/>).
/// </summary>
/// <remarks>
/// These are REQUEST-shape failures, so every one of them maps to HTTP 400: a report reads data,
/// it never conflicts with state, and a query against a company that does not exist simply
/// answers 200 with empty sections rather than 404 (same read semantics as the account tree).
/// </remarks>
public static class FinancialReportErrorCodes
{
    /// <summary>
    /// <c>companyId</c> missing or <c>Guid.Empty</c>. Wire value is shared with
    /// <see cref="AccountErrorCodes.CompanyRequired"/> on purpose: the machine code names the
    /// CONDITION ("the request needs a company"), not the endpoint that caught it.
    /// </summary>
    public const string CompanyRequired = "company_required";

    /// <summary>
    /// A REQUIRED date parameter of a statement endpoint (<c>asOfDate</c> for trial-balance and
    /// balance-sheet, <c>from</c>/<c>to</c> for profit-and-loss) was not sent at all: the caller
    /// cannot want "some date" - the period IS the report.
    /// </summary>
    public const string DateRequired = "date_required";

    /// <summary>
    /// A date parameter was sent but is not a parseable calendar date. The pinned contract spells
    /// dates as <c>yyyy-MM-dd</c>; anything the framework cannot parse is rejected instead of
    /// silently shifting the report to another period.
    /// </summary>
    public const string InvalidDate = "invalid_date";
}
