using Erp.Domain.Entities;

namespace Erp.Application.DTOs;

/// <summary>Asset category payload (Task 10.1): GL account template links.</summary>
public sealed record AssetCategoryDto(
    Guid Id,
    Guid CompanyId,
    string CategoryName,
    Guid FixedAssetAccountId,
    Guid AccumulatedDepreciationAccountId,
    Guid DepreciationExpenseAccountId,
    Guid? CwipAccountId,
    Guid? GainOnDisposalAccountId,
    Guid? LossOnDisposalAccountId,
    bool IsNonDepreciable,
    bool IsActive,
    DateTimeOffset CreatedAt)
{
    public static AssetCategoryDto Build(AssetCategory category) =>
        new(
            category.Id,
            category.CompanyId,
            category.CategoryName,
            category.FixedAssetAccountId,
            category.AccumulatedDepreciationAccountId,
            category.DepreciationExpenseAccountId,
            category.CwipAccountId,
            category.GainOnDisposalAccountId,
            category.LossOnDisposalAccountId,
            category.IsNonDepreciable,
            category.IsActive,
            category.CreatedAt);
}

/// <summary>One schedule line payload (Task 10.3).</summary>
public sealed record AssetScheduleLineDto(
    Guid Id,
    DateOnly ScheduleDate,
    decimal DepreciationAmount,
    decimal AccumulatedDepreciationAfter,
    AssetScheduleStatus Status);

/// <summary>Asset payload (Task 10.2): master fields plus the live Net Book Value.</summary>
public sealed record AssetDto(
    Guid Id,
    Guid CompanyId,
    string AssetCode,
    string AssetName,
    Guid ItemId,
    Guid AssetCategoryId,
    DateOnly PurchaseDate,
    DateOnly AvailableForUseDate,
    decimal GrossPurchaseAmount,
    decimal SalvageValue,
    decimal AccumulatedDepreciation,
    decimal NetBookValue,
    DepreciationMethod DepreciationMethod,
    int TotalNumberOfDepreciations,
    int FrequencyInMonths,
    AssetStatus Status,
    DateOnly? DisposalDate,
    DateTimeOffset CreatedAt)
{
    public static AssetDto Build(Asset asset) =>
        new(
            asset.Id,
            asset.CompanyId,
            asset.AssetCode,
            asset.AssetName,
            asset.ItemId,
            asset.AssetCategoryId,
            asset.PurchaseDate,
            asset.AvailableForUseDate,
            asset.GrossPurchaseAmount,
            asset.SalvageValue,
            asset.AccumulatedDepreciation,
            asset.NetBookValue,
            asset.DepreciationMethod,
            asset.TotalNumberOfDepreciations,
            asset.FrequencyInMonths,
            asset.Status,
            asset.DisposalDate,
            asset.CreatedAt);
}

/// <summary>
/// Capitalization result (Task 10.2 acceptance: "generates initial balance sheet records"):
/// the capitalized asset plus the generated schedule summary.
/// </summary>
public sealed record AssetCapitalizationDto(
    AssetDto Asset,
    IReadOnlyList<AssetScheduleLineDto> Schedule,
    int ScheduleCount,
    decimal TotalScheduled);

/// <summary>
/// Asset detail payload (Block B reads): the master plus its schedule lines ordered by due date.
/// </summary>
public sealed record AssetDetailDto(
    AssetDto Asset,
    IReadOnlyList<AssetScheduleLineDto> Schedule);

/// <summary>
/// One skipped schedule line of a depreciation run (Task 10.4): the row identity travels with
/// the reason so a live replay test can assert the exact rows that were left untouched.
/// </summary>
public sealed record DepreciationSkipDto(
    Guid ScheduleLineId,
    Guid AssetId,
    string Reason);

/// <summary>
/// Periodic depreciation run result (Task 10.4 acceptance): how many lines booked into the
/// single batch voucher, the booked total, and every skipped row with its reason.
/// An empty due set succeeds with BookedCount 0, no voucher and no skips.
/// </summary>
public sealed record DepreciationRunDto(
    string? VoucherNo,
    int BookedCount,
    decimal TotalBooked,
    IReadOnlyList<DepreciationSkipDto> Skipped)
{
    public int SkippedCount => Skipped.Count;
}

/// <summary>
/// Disposal result (Task 10.5 acceptance): the terminal asset, the balancing voucher and the
/// economics of the exit. <see cref="NetGainLoss"/> is proceeds − NBV: positive is a gain
/// (credited to the gain account), negative a loss (debited to the loss account), zero a
/// break-even with neither line.
/// </summary>
public sealed record AssetDisposalDto(
    AssetDto Asset,
    string VoucherNo,
    decimal ProceedsAmount,
    decimal NetGainLoss,
    int CancelledFutureLines);

/// <summary>
/// Disposal reversal result (Task 10.6, spec AS-05 reversal): the restored asset, the reversal
/// voucher, and the number of schedule lines reopened to Scheduled. <see cref="ReopenedLines"/>
/// counts the Cancelled lines that were restored.
/// </summary>
public sealed record AssetDisposalReversalDto(
    AssetDto Asset,
    string ReversalVoucherNo,
    int ReopenedLines);
