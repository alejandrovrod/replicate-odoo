using Erp.Domain.Common;
using Erp.Domain.Exceptions;

namespace Erp.Domain.Entities;

public enum DocumentStatus
{
    Draft,
    Submitted,
    Cancelled
}

/// <summary>
/// Submittable year-end closing voucher (spec §1, R-13): bound to exactly one open
/// <see cref="FiscalYear"/>, it zeroes every Profit &amp; Loss leaf inside that year and transfers
/// the net result to a single retained earnings Equity leaf in one atomic posting.
/// Lifecycle: <c>Draft → Submitted → Cancelled</c> only (spec FC-05).
/// </summary>
public class PeriodClosingVoucher : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>
    /// Human number, <c>PCV-&lt;yyyy&gt;-&lt;seq&gt;</c>, assigned inside the submit transaction
    /// (plan.md §6; full gapless series deferred to R-31).
    /// </summary>
    public string VoucherNo { get; set; } = string.Empty;
    public Guid CompanyId { get; set; }

    public Company? Company { get; set; }

    /// <summary>
    /// The fiscal year being closed (spec FC-04). Required: without it containment is unmodellable.
    /// </summary>
    public Guid FiscalYearId { get; set; }

    public FiscalYear? FiscalYear { get; set; }

    /// <summary>Must fall inside <c>[FiscalYear.StartDate, FiscalYear.EndDate]</c> (spec FC-04).</summary>
    public DateOnly PostingDate { get; set; }
    public Guid RetainedEarningsAccountId { get; set; }

    public Account? RetainedEarningsAccount { get; set; }

    public DocumentStatus DocumentStatus { get; set; }

    public string? Remarks { get; set; }

    /// <summary>
    /// Client-supplied idempotency token (spec FC-05): a replayed submit carrying the same key
    /// after commit returns the recorded success without new GL rows.
    /// </summary>
    public string? IdempotencyKey { get; set; }

    /// <summary>Derived line snapshot written at submit (plan.md §2.3 audit).</summary>
    public ICollection<PeriodClosingVoucherLine> Lines { get; set; } = new List<PeriodClosingVoucherLine>();

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Guards the submit transition (spec FC-05): only <c>Draft → Submitted</c> is legal.
    /// A replay of an already-submitted voucher carrying the SAME idempotency key is NOT an
    /// error — the handler short-circuits it to the recorded success before reaching this guard.
    /// </summary>
    /// <exception cref="FiscalClosingException">
    /// <c>period_closing_invalid_transition</c> or <c>period_closing_already_cancelled</c>.
    /// </exception>
    public void EnsureCanSubmit()
    {
        if (DocumentStatus == DocumentStatus.Cancelled)
        {
            throw new FiscalClosingTransitionException(
                FiscalClosingErrorCodes.PeriodClosingAlreadyCancelled,
                $"Closing voucher '{VoucherNo}' is already cancelled and can never be submitted again (spec FC-05).");
        }

        if (DocumentStatus != DocumentStatus.Draft)
        {
            throw new FiscalClosingTransitionException(
                FiscalClosingErrorCodes.PeriodClosingInvalidTransition,
                $"Closing voucher '{VoucherNo}' cannot be submitted from status '{DocumentStatus}': "
                + "only Draft → Submitted is legal (spec FC-05).");
        }
    }

    /// <summary>
    /// Guards the cancel transition (spec FC-05/FC-06): only <c>Submitted → Cancelled</c> is legal.
    /// </summary>
    /// <exception cref="FiscalClosingException">
    /// <c>period_closing_invalid_transition</c> or <c>period_closing_already_cancelled</c>.
    /// </exception>
    public void EnsureCanCancel()
    {
        if (DocumentStatus == DocumentStatus.Cancelled)
        {
            throw new FiscalClosingTransitionException(
                FiscalClosingErrorCodes.PeriodClosingAlreadyCancelled,
                $"Closing voucher '{VoucherNo}' is already cancelled (spec FC-05).");
        }

        if (DocumentStatus != DocumentStatus.Submitted)
        {
            throw new FiscalClosingTransitionException(
                FiscalClosingErrorCodes.PeriodClosingInvalidTransition,
                $"Closing voucher '{VoucherNo}' cannot be cancelled from status '{DocumentStatus}': "
                + "only Submitted → Cancelled is legal (spec FC-05/FC-06).");
        }
    }

    /// <summary>Rejects an unbound voucher: every closing belongs to exactly one fiscal year.</summary>
    /// <exception cref="ClosingDateOutsideFiscalYearException">No fiscal year bound.</exception>
    public void EnsureFiscalYearBound()
    {
        if (FiscalYearId == Guid.Empty)
        {
            throw new ClosingDateOutsideFiscalYearException(PostingDate, "(unbound)", default, default);
        }
    }
}

/// <summary>
/// Lifecycle-transition failure of the closing voucher (spec FC-05). Carries either
/// <c>period_closing_invalid_transition</c> or <c>period_closing_already_cancelled</c> (both HTTP 409).
/// </summary>
public sealed class FiscalClosingTransitionException : FiscalClosingException
{
    public FiscalClosingTransitionException(string code, string message)
        : base(code, message)
    {
    }
}
