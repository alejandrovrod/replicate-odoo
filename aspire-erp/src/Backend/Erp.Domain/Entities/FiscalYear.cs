using Erp.Domain.Common;
using Erp.Domain.Exceptions;

namespace Erp.Domain.Entities;

/// <summary>
/// Named year boundary per company (spec §1 ubiquitous language, R-13). A fiscal year bounds one
/// accounting year: the <see cref="PeriodClosingVoucher"/> zeroes every P&amp;L leaf inside
/// <c>[StartDate, EndDate]</c> and the hard period lock refuses any posting into a closed year.
/// </summary>
/// <remarks>
/// Tenant-scoped (<see cref="ITenantEntity"/>). Closing sets <c>IsClosed = 1</c>; re-open is
/// FORBIDDEN in this change (no re-open method exists — manual DBA escape hatch only, out of
/// scope per spec §6).
/// </remarks>
public class FiscalYear : ITenantEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant; immutable after creation.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Company that owns this fiscal calendar (overlap scope is per-company).</summary>
    public Guid CompanyId { get; set; }

    public Company? Company { get; set; }

    /// <summary>Display name, e.g. 'FY-2025'. Unique per company.</summary>
    public string YearName { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    /// <summary>Hard lock flag: a closed year is immutable.</summary>
    public bool IsClosed { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    /// <summary>Optimistic concurrency token (SQL Server rowversion).</summary>
    public byte[] RowVersion { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Validates the year boundary (spec FC-04: <c>StartDate &lt; EndDate</c>; empty name rejected).
    /// </summary>
    /// <exception cref="ArgumentException">Boundary or name invalid.</exception>
    public void EnsureValidBoundary()
    {
        if (string.IsNullOrWhiteSpace(YearName))
        {
            throw new ArgumentException("Fiscal year name is required.", nameof(YearName));
        }

        if (StartDate >= EndDate)
        {
            throw new ArgumentException(
                $"Fiscal year '{YearName}': StartDate ({StartDate:yyyy-MM-dd}) must be strictly "
                + $"before EndDate ({EndDate:yyyy-MM-dd}) (spec FC-04).",
                nameof(StartDate));
        }
    }

    /// <summary>
    /// Plan.md §3 hard-lock rule, second line: refuses any posting date covered by this year when
    /// the year is closed. Dates outside <c>[StartDate, EndDate]</c> are NOT this year's concern —
    /// the caller resolves the covering year first (repository helper
    /// <c>GetCoveringYearAsync</c>); a null covering year means open.
    /// </summary>
    /// <exception cref="ClosingDateOutsideFiscalYearException">
    /// The date is outside this year's window and this instance cannot judge it.
    /// </exception>
    /// <exception cref="FiscalYearClosedException">The date falls in this closed year.</exception>
    public void EnsurePostingAllowed(DateOnly postingDate)
    {
        if (postingDate < StartDate || postingDate > EndDate)
        {
            throw new ClosingDateOutsideFiscalYearException(postingDate, YearName, StartDate, EndDate);
        }

        if (IsClosed)
        {
            throw new FiscalYearClosedException(Id, YearName);
        }
    }

    /// <summary>
    /// Whether this year overlaps the given window (same-company overlap check, spec FC-04).
    /// Touching boundaries do NOT overlap: <c>[2025-01-01, 2025-12-31]</c> and
    /// <c>[2026-01-01, 2026-12-31]</c> are adjacent, not overlapping.
    /// </summary>
    public bool Overlaps(DateOnly startDate, DateOnly endDate) =>
        StartDate <= endDate && startDate <= EndDate;

    /// <summary>
    /// Hard-locks the year (spec FC-04). Refuses an already-closed year; there is no re-open.
    /// </summary>
    /// <exception cref="FiscalYearClosedException">The year is already closed.</exception>
    public void Close()
    {
        if (IsClosed)
        {
            throw new FiscalYearClosedException(Id, YearName);
        }

        IsClosed = true;
        ClosedAt = DateTimeOffset.UtcNow;
    }
}
