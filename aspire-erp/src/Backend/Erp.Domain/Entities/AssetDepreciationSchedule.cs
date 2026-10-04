namespace Erp.Domain.Entities;

/// <summary>
/// Lifecycle of one depreciation schedule line (Task 10.3): Scheduled (planned, unposted) -&gt;
/// Booked (the Block B periodic worker posted it, AS-02/AS-04) or Cancelled (disposal wiped the
/// future lines, AS-05; restorable by the Block C reversal, Task 10.6).
/// Persisted as the enum NAME (NVARCHAR(20)), matching the RootType precedent from plan.md §7.3.
/// </summary>
public enum AssetScheduleStatus
{
    Scheduled,
    Booked,
    Cancelled,
}

/// <summary>
/// One planned depreciation posting of an <see cref="Asset"/> (Task 10.3, plan.md §1 DDL table 3,
/// spec "Depreciation Schedule"): due date, amount and the running accumulated total after it.
/// </summary>
/// <remarks>
/// <para>
/// Line of the Asset aggregate (mirrors <see cref="StockEntryItem"/>): no TenantId of its own,
/// cascade-deleted with the asset. Deliberately NOT temporal - like the Kardex, schedule lines
/// are an append-only time series, not a master entity.
/// </para>
/// <para>
/// Deviation from plan DDL: <see cref="Status"/> REPLACES the plan's <c>IsBooked BIT</c>. A bit
/// cannot represent AS-05's cancelled future lines nor the 10.6 reversal restore; AS-04's
/// "IsBooked = 1" maps to <c>Status == Booked</c>. <see cref="JournalEntryId"/> (plan DDL) links
/// the Block B booking voucher.
/// </para>
/// </remarks>
public class AssetDepreciationSchedule
{
    public Guid Id { get; set; }

    public Guid AssetId { get; set; }

    public Asset? Asset { get; set; }

    /// <summary>Due date of this depreciation posting.</summary>
    public DateOnly ScheduleDate { get; set; }

    /// <summary>Amount to post (decimal(18,4), strictly positive - CK_DepSchedule_Amount).</summary>
    public decimal DepreciationAmount { get; set; }

    /// <summary>Asset-level accumulated depreciation immediately after this line posts.</summary>
    public decimal AccumulatedDepreciationAfter { get; set; }

    public AssetScheduleStatus Status { get; set; } = AssetScheduleStatus.Scheduled;

    /// <summary>Block B booking voucher (journal entry) that posted this line; null until booked.</summary>
    public Guid? JournalEntryId { get; set; }
}
