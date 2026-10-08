using Erp.Domain.Entities;

namespace Erp.Application.Features.FiscalClosing;

/// <summary>
/// RFC 7807 status mapping for every spec §5 code (plan.md §5). Lives in Application (not Api)
/// so the code ↔ status contract is unit-testable without the web stack; the controllers
/// delegate to it.
/// </summary>
public static class FiscalClosingHttpStatus
{
    public static int StatusFor(string code) => code switch
    {
        FiscalClosingErrorCodes.FiscalYearClosed
            or FiscalClosingErrorCodes.FiscalYearOverlap
            or FiscalClosingErrorCodes.DuplicateClosingForFiscalYear
            or AccountingErrorCodes.FiscalPeriodLocked
            or FiscalClosingErrorCodes.PeriodClosingInvalidTransition
            or FiscalClosingErrorCodes.PeriodClosingAlreadyCancelled
            or FiscalClosingErrorCodes.ConcurrencyConflict => 409,
        FiscalClosingErrorCodes.InvalidRetainedEarningsAccount => 400,
        FiscalClosingErrorCodes.ClosingDateOutsideFiscalYear
            or FiscalClosingErrorCodes.NoClosingBalances => 422,
        FiscalClosingErrorCodes.PeriodClosingNotFound
            or FiscalClosingErrorCodes.FiscalYearNotFound => 404,
        FiscalClosingErrorCodes.ClosingNonPLAccount
            or FiscalClosingErrorCodes.DoubleEntryImbalance => 500,
        _ => 400,
    };
}
