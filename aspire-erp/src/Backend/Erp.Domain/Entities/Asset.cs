using Erp.Domain.Common;
using Erp.Domain.Exceptions;

namespace Erp.Domain.Entities;

/// <summary>
/// Depreciation method of an <see cref="Asset"/>. Only straight-line is implemented (spec AS-01,
/// Tasks 10.1-10.3): the plan glossary mentions declining-balance, but no task builds it -
/// persisting any other value stays a follow-up, not a silent half-implementation.
/// Persisted as the enum NAME (NVARCHAR(30)), matching the RootType precedent from plan.md §7.3.
/// </summary>
public enum DepreciationMethod
{
    StraightLine,
}

/// <summary>
/// Capitalization lifecycle of an <see cref="Asset"/> (plan.md §1 DDL Status values):
/// Draft -&gt; Submitted -&gt; Capitalized -&gt; FullyDepreciated, with Sold / Scrapped as the
/// Block B (Task 10.5) terminal states. Persisted as the enum NAME (NVARCHAR(30)) per the plan
/// DDL - unlike WorkOrder, whose DDL carries no such column type and follows the integer
/// PurchaseOrder precedent.
/// </summary>
public enum AssetStatus
{
    Draft,
    Submitted,
    Capitalized,
    FullyDepreciated,
    Sold,
    Scrapped,
}

/// <summary>
/// Fixed asset master (Task 10.2, plan.md §1 DDL table 2, spec ubiquitous language "Asset"):
/// identification, gross purchase value, salvage floor and the straight-line schedule sizing
/// (useful life = <see cref="TotalNumberOfDepreciations"/> x <see cref="FrequencyInMonths"/>).
/// </summary>
/// <remarks>
/// Tenant-scoped (<see cref="ITenantEntity"/>) and temporal like <see cref="Account"/>.
/// The gapless <see cref="AssetCode"/> (AST-YYYY-NNNNN) is assigned inside the capitalization
/// transaction, mirroring the WO/MF numbering (Constitution III.4): a rollback consumes no
/// number. <see cref="AccumulatedDepreciation"/> is persisted (default 0) so NBV survives
/// reloads; <see cref="NetBookValue"/> itself is a pure computed getter (never mapped).
/// </remarks>
public class Asset : ITenantEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant; immutable after creation (Constitution Article II.4).</summary>
    public Guid TenantId { get; set; }

    public Guid CompanyId { get; set; }

    /// <summary>Gapless voucher number (Constitution III.4): AST-2026-00001, assigned at creation.</summary>
    public string AssetCode { get; set; } = string.Empty;

    /// <summary>Display name, e.g. "Dell Precision Laptop" (max 150 chars, plan.md §1).</summary>
    public string AssetName { get; set; } = string.Empty;

    public Guid ItemId { get; set; }

    public Item? Item { get; set; }

    public Guid AssetCategoryId { get; set; }

    public AssetCategory? AssetCategory { get; set; }

    public DateOnly PurchaseDate { get; set; }

    /// <summary>Date the asset is placed in service; the first schedule line falls one frequency
    /// after this date (Task 10.3 engine: AddMonths(i * frequency), i = 1..n).</summary>
    public DateOnly AvailableForUseDate { get; set; }

    /// <summary>Original capitalized cost (decimal(18,4), strictly positive - CK_Asset_Values).</summary>
    public decimal GrossPurchaseAmount { get; set; }

    /// <summary>Residual floor the NBV can never depreciate below (AS-01: NBV &gt;= SalvageValue).</summary>
    public decimal SalvageValue { get; set; }

    /// <summary>Depreciation booked so far (decimal(18,4), &gt;= 0); 0 until Block B posts lines.</summary>
    public decimal AccumulatedDepreciation { get; set; }

    /// <summary>StraightLine only (see <see cref="DepreciationMethod"/>).</summary>
    public DepreciationMethod DepreciationMethod { get; set; } = DepreciationMethod.StraightLine;

    /// <summary>Useful life expressed as a count of depreciation postings (&gt; 0).</summary>
    public int TotalNumberOfDepreciations { get; set; }

    /// <summary>Months between postings (&gt; 0, default 1 = monthly).</summary>
    public int FrequencyInMonths { get; set; } = 1;

    /// <summary>Workflow state (Task 10.2 acceptance: Draft/Submitted -&gt; Capitalized).</summary>
    public AssetStatus Status { get; set; } = AssetStatus.Draft;

    /// <summary>
    /// Disposal date (Task 10.5): stamped when the asset reaches its terminal Sold/Scrapped state,
    /// null until then. Additive column (the Block C migration adds it); the accounting date of
    /// the disposal voucher.
    /// </summary>
    public DateOnly? DisposalDate { get; set; }

    /// <summary>
    /// Proceeds amount from the disposal (Task 10.5): positive for sales, zero for scraps.
    /// Used by the reversal logic to reconstruct the original GL lines.
    /// </summary>
    public decimal DisposalProceedsAmount { get; set; }

    /// <summary>
    /// Voucher number of the disposal that led to this asset's current state.
    /// Used by the reversal logic to find the original GL lines.
    /// </summary>
    public string DisposalVoucherNo { get; set; } = string.Empty;

    /// <summary>
    /// Optimistic concurrency token (SQL Server <c>rowversion</c> - spec AS-06): capitalization
    /// and the Block B disposal/depreciation race are read-modify-write, so EF puts the original
    /// value in the UPDATE ... WHERE clause and a concurrent transition throws
    /// <c>DbUpdateConcurrencyException</c> instead of being silently lost.
    /// Store-generated: never set from code.
    /// </summary>
    public byte[] RowVersion { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Remaining unamortized carrying value (spec glossary NBV): Gross − Accumulated.
    /// Pure computed getter, explicitly ignored by the EF configuration - never mapped, so there
    /// is no model/store risk (EF ignores getter-only properties; the Ignore call documents it).
    /// </summary>
    public decimal NetBookValue => GrossPurchaseAmount - AccumulatedDepreciation;

    /// <summary>
    /// Draft/Submitted -&gt; Capitalized (Task 10.2). Called by the capitalize handler only after
    /// the CWIP clearing voucher balances and the schedule lines are built.
    /// </summary>
    /// <exception cref="AssetValidationException">The asset is not capitalizable (<c>invalid_status_transition</c>).</exception>
    public void Capitalize()
    {
        if (Status is not (AssetStatus.Draft or AssetStatus.Submitted))
        {
            throw new AssetValidationException(
                AssetErrorCodes.InvalidStatusTransition,
                $"Only a Draft or Submitted asset can be capitalized; asset '{AssetCode}' is '{Status}'.");
        }

        Status = AssetStatus.Capitalized;
    }

    /// <summary>
    /// Capitalized -&gt; FullyDepreciated (Task 10.4): the periodic run calls this when no
    /// Scheduled lines remain and the accumulated depreciation equals the depreciable base
    /// (gross − salvage), so the NBV rests exactly on the salvage floor (AS-01).
    /// </summary>
    /// <exception cref="AssetValidationException">The asset is not depreciable (<c>invalid_status_transition</c>).</exception>
    public void MarkFullyDepreciated()
    {
        if (Status is not AssetStatus.Capitalized)
        {
            throw new AssetValidationException(
                AssetErrorCodes.InvalidStatusTransition,
                $"Only a Capitalized asset can become FullyDepreciated; asset '{AssetCode}' is '{Status}'.");
        }

        Status = AssetStatus.FullyDepreciated;
    }

    /// <summary>
    /// Capitalized/FullyDepreciated -&gt; Sold (proceeds &gt; 0) or Scrapped (proceeds == 0)
    /// (Task 10.5, scenarios AS-03/AS-05). Draft/Submitted/Sold/Scrapped holders fail with
    /// <c>invalid_status_transition</c> - including the double-disposal replay.
    /// </summary>
    /// <exception cref="AssetValidationException">The asset is not disposable (<c>invalid_status_transition</c>).</exception>
    public void Dispose(AssetStatus terminalStatus, DateOnly disposalDate)
    {
        if (Status is not (AssetStatus.Capitalized or AssetStatus.FullyDepreciated))
        {
            throw new AssetValidationException(
                AssetErrorCodes.InvalidStatusTransition,
                $"Only a Capitalized or FullyDepreciated asset can be disposed; asset '{AssetCode}' is '{Status}'.");
        }

        if (terminalStatus is not (AssetStatus.Sold or AssetStatus.Scrapped))
        {
            throw new AssetValidationException(
                AssetErrorCodes.InvalidStatusTransition,
                $"Disposal must land in Sold or Scrapped (received '{terminalStatus}').");
        }

        Status = terminalStatus;
        DisposalDate = disposalDate;
    }

    /// <summary>
    /// Reverses a disposal (Task 10.6 / spec AS-05 reversal): restores the asset to
    /// Capitalized (or FullyDepreciated) by clearing DisposalDate, resetting status,
    /// and clearing disposal proceeds. Called by the cancel handler after the GL
    /// reversal and schedule line reopening are done.
    /// </summary>
    public void UndoDisposal(AssetStatus restoredStatus)
    {
        if (Status is not (AssetStatus.Sold or AssetStatus.Scrapped))
        {
            throw new AssetValidationException(
                AssetErrorCodes.InvalidStatusTransition,
                $"Only a Sold or Scrapped asset can have its disposal cancelled; asset '{AssetCode}' is '{Status}'.");
        }

        if (restoredStatus is not (AssetStatus.Capitalized or AssetStatus.FullyDepreciated))
        {
            throw new AssetValidationException(
                AssetErrorCodes.InvalidStatusTransition,
                $"Reversal must land in Capitalized or FullyDepreciated (received '{restoredStatus}').");
        }

        Status = restoredStatus;
        DisposalDate = null;
        DisposalProceedsAmount = 0m;
    }
}
