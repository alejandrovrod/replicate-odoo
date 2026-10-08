using Erp.Domain.Entities;

namespace Erp.Domain.Exceptions;

/// <summary>
/// Base class for the Fiscal Closing (R-13) typed failures. Each carries the stable
/// <see cref="Code"/> from <c>Erp.Domain.Entities.FiscalClosingErrorCodes</c> so upper layers
/// can map the failure to an RFC 7807 response without string-matching messages.
/// </summary>
public abstract class FiscalClosingException : Exception
{
    public string Code { get; }

    protected FiscalClosingException(string code, string message)
        : base(message)
    {
        Code = code;
    }
}

/// <summary>Posting targets a fiscal year with <c>IsClosed = true</c> (spec FC-04, HTTP 409).</summary>
public sealed class FiscalYearClosedException : FiscalClosingException
{
    public Guid FiscalYearId { get; }

    public FiscalYearClosedException(Guid fiscalYearId, string yearName)
        : base(
            FiscalClosingErrorCodes.FiscalYearClosed,
            $"Fiscal year '{yearName}' is closed and immutable: no voucher may be created, "
            + "submitted or cancelled in it (spec FC-04). Re-open is out of scope.")
    {
        FiscalYearId = fiscalYearId;
    }
}

/// <summary>New fiscal year overlaps an existing year of the same company (spec FC-04, HTTP 409).</summary>
public sealed class FiscalYearOverlapException : FiscalClosingException
{
    public FiscalYearOverlapException(string yearName, DateOnly startDate, DateOnly endDate)
        : base(
            FiscalClosingErrorCodes.FiscalYearOverlap,
            $"Fiscal year '{yearName}' ({startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}) overlaps "
            + "an existing fiscal year of the same company (spec FC-04).")
    {
    }
}

/// <summary>
/// The retained earnings account fails the FC-03 validity gate (HTTP 400).
/// </summary>
public sealed class InvalidRetainedEarningsException : FiscalClosingException
{
    public Guid AccountId { get; }

    public InvalidRetainedEarningsException(Guid accountId, string reason)
        : base(
            FiscalClosingErrorCodes.InvalidRetainedEarningsAccount,
            $"Retained earnings account '{accountId}' is invalid: {reason} "
            + "(spec FC-03: must exist, belong to the same tenant and company, be an active "
            + "non-group Equity leaf).")
    {
        AccountId = accountId;
    }
}

/// <summary>Voucher <c>PostingDate ∉ [StartDate, EndDate]</c> of its fiscal year (spec FC-04, HTTP 422).</summary>
public sealed class ClosingDateOutsideFiscalYearException : FiscalClosingException
{
    public ClosingDateOutsideFiscalYearException(DateOnly postingDate, string yearName, DateOnly startDate, DateOnly endDate)
        : base(
            FiscalClosingErrorCodes.ClosingDateOutsideFiscalYear,
            $"Posting date {postingDate:yyyy-MM-dd} falls outside fiscal year '{yearName}' "
            + $"({startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}) (spec FC-04).")
    {
    }
}

/// <summary>
/// A submitted non-cancelled closing voucher already exists for the year (spec FC-05, HTTP 409).
/// </summary>
public sealed class DuplicateClosingForFiscalYearException : FiscalClosingException
{
    public Guid FiscalYearId { get; }

    public DuplicateClosingForFiscalYearException(Guid fiscalYearId)
        : base(
            FiscalClosingErrorCodes.DuplicateClosingForFiscalYear,
            "A submitted closing voucher already exists for this fiscal year: exactly one live "
            + "close per year is allowed (spec FC-05).")
    {
        FiscalYearId = fiscalYearId;
    }
}

/// <summary>Zero P&amp;L balances in the fiscal-year window (spec FC-05, HTTP 422).</summary>
public sealed class NoClosingBalancesException : FiscalClosingException
{
    public NoClosingBalancesException(string yearName)
        : base(
            FiscalClosingErrorCodes.NoClosingBalances,
            $"Fiscal year '{yearName}' has no non-zero Profit & Loss balances to close: an empty "
            + "year is rejected, never silently submitted (spec FC-12).")
    {
    }
}

/// <summary>
/// The closing engine attempted to zero a non-P&amp;L account — defense-in-depth tripwire
/// signalling an internal bug (spec FC-02, HTTP 500).
/// </summary>
public sealed class ClosingNonPLAccountException : FiscalClosingException
{
    public Guid AccountId { get; }

    public ClosingNonPLAccountException(Guid accountId, string rootType)
        : base(
            FiscalClosingErrorCodes.ClosingNonPLAccount,
            $"Account '{accountId}' (RootType '{rootType}') is not a Profit & Loss leaf and must "
            + "never receive a closing line: Balance Sheet accounts are untouched by the close "
            + "(spec FC-02).")
    {
        AccountId = accountId;
    }
}
