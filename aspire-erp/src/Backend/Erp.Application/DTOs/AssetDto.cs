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
