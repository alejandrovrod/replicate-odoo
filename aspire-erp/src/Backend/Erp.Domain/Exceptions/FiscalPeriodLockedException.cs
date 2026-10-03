using Erp.Domain.Entities;

namespace Erp.Domain.Exceptions;

/// <summary>
/// Hard Fiscal Period Lock (spec AC-04 / constitution-adjacent plan.md §3): when a company has a
/// <c>FrozenAccountsDate</c>, ANY posting dated on or before that day is rejected - the books of
/// a closed period must never change again. Thrown by
/// <see cref="Company.EnsurePostingDateUnlocked"/> BEFORE a single GLEntry line is built, so a
/// rejected back-dated attempt modifies zero data (AC-04: "no data is modified").
/// </summary>
/// <remarks>
/// Carries both dates so the RFC 7807 <c>detail</c> can tell the user exactly which boundary
/// closed the period. The <see cref="Code"/> travels Domain -&gt; Application
/// (<c>Result.Failure</c>) -&gt; Api, where the posting controllers map it to HTTP 409.
/// </remarks>
public sealed class FiscalPeriodLockedException : Exception
{
    /// <summary>Stable failure code carried into the RFC 7807 <c>code</c> extension.</summary>
    public string Code { get; } = AccountingErrorCodes.FiscalPeriodLocked;

    /// <summary>Posting date that was rejected.</summary>
    public DateOnly PostingDate { get; }

    /// <summary>Company freeze boundary that rejected it (the period is closed on this day).</summary>
    public DateOnly FrozenAccountsDate { get; }

    public FiscalPeriodLockedException(DateOnly postingDate, DateOnly frozenAccountsDate)
        : base(
            $"Fiscal period locked: posting date {postingDate:yyyy-MM-dd} is on or before "
            + $"FrozenAccountsDate {frozenAccountsDate:yyyy-MM-dd} of the company, so the period "
            + "is closed and no voucher may be posted, modified or cancelled in it "
            + "(spec AC-04 / plan.md §3).")
    {
        PostingDate = postingDate;
        FrozenAccountsDate = frozenAccountsDate;
    }
}
