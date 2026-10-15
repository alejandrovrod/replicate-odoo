using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using Erp.Domain.Services;

namespace Erp.Application.Features.Assets.Commands;

/// <summary>
/// Executes <see cref="GenerateAssetScheduleCommand"/>: resolves the asset and category, runs the
/// value/period guards, refuses to touch an asset that already has schedule lines, and writes the
/// straight-line calendar - all inside ONE <c>IAssetsRepository</c> transaction. No General Ledger
/// rows are produced (the schedule is a plan).
/// </summary>
public sealed class GenerateAssetScheduleCommandHandler
    : ICommandHandler<GenerateAssetScheduleCommand, Result<AssetCapitalizationDto>>
{
    private readonly IAssetsRepository _assets;

    public GenerateAssetScheduleCommandHandler(IAssetsRepository assets)
    {
        _assets = assets;
    }

    public async Task<Result<AssetCapitalizationDto>> HandleAsync(
        GenerateAssetScheduleCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _assets.ExecuteInTransactionAsync(async token =>
            {
                var asset = await _assets.GetAssetByIdAsync(command.AssetId, token)
                    ?? throw new AssetValidationException(
                        AssetErrorCodes.AssetNotFound,
                        $"Asset '{command.AssetId}' was not found in this tenant.");

                // Company mismatch is reported as NOT FOUND so the id never leaks across companies.
                if (asset.CompanyId != command.CompanyId)
                {
                    throw new AssetValidationException(
                        AssetErrorCodes.AssetNotFound,
                        $"Asset '{command.AssetId}' does not belong to company '{command.CompanyId}'.");
                }

                if (asset.Status is AssetStatus.Sold or AssetStatus.Scrapped or AssetStatus.Cancelled)
                {
                    throw new AssetValidationException(
                        AssetErrorCodes.InvalidStatusTransition,
                        $"A {asset.Status} asset cannot generate a depreciation schedule.");
                }

                var existing = await _assets.GetSchedulesByAssetAsync(asset.Id, token);
                if (existing.Count > 0)
                {
                    throw new AssetValidationException(
                        AssetErrorCodes.ScheduleAlreadyExists,
                        $"Asset '{asset.AssetCode}' already has {existing.Count} schedule line(s); "
                        + "regeneration is not supported once the calendar exists.");
                }

                var category = await _assets.GetCategoryByIdAsync(asset.AssetCategoryId, token)
                    ?? throw new AssetValidationException(
                        AssetErrorCodes.CategoryNotFound,
                        $"Asset category '{asset.AssetCategoryId}' was not found in this tenant.");

                if (category.CompanyId != command.CompanyId)
                {
                    throw new AssetValidationException(
                        AssetErrorCodes.CategoryNotFound,
                        $"Asset category '{category.CategoryName}' does not belong to company '{command.CompanyId}'.");
                }

                AssetValidator.EnsureValidGrossAmount(asset.GrossPurchaseAmount);
                AssetValidator.EnsureValidSalvageValue(asset.GrossPurchaseAmount, asset.SalvageValue);
                AssetValidator.EnsureValidDepreciationPeriods(
                    asset.TotalNumberOfDepreciations, asset.FrequencyInMonths);

                var lines = new List<AssetDepreciationSchedule>();
                if (!category.IsNonDepreciable)
                {
                    var planned = DepreciationScheduler.GenerateStraightLineSchedule(
                        asset.GrossPurchaseAmount,
                        asset.SalvageValue,
                        asset.TotalNumberOfDepreciations,
                        asset.FrequencyInMonths,
                        asset.AvailableForUseDate);

                    foreach (var row in planned)
                    {
                        lines.Add(new AssetDepreciationSchedule
                        {
                            Id = Guid.NewGuid(),
                            AssetId = asset.Id,
                            ScheduleDate = row.ScheduleDate,
                            DepreciationAmount = row.DepreciationAmount,
                            AccumulatedDepreciationAfter = row.AccumulatedDepreciation,
                            Status = AssetScheduleStatus.Scheduled,
                        });
                    }
                }

                if (lines.Count > 0)
                {
                    await _assets.AddScheduleRangeAsync(lines, token);
                }

                var lineDtos = lines
                    .OrderBy(l => l.ScheduleDate)
                    .Select(l => new AssetScheduleLineDto(
                        l.Id, l.ScheduleDate, l.DepreciationAmount, l.AccumulatedDepreciationAfter, l.Status))
                    .ToList();

                return Result<AssetCapitalizationDto>.Success(new AssetCapitalizationDto(
                    AssetDto.Build(asset),
                    lineDtos,
                    lineDtos.Count,
                    Round4(lineDtos.Sum(l => l.DepreciationAmount))));
            }, cancellationToken);
        }
        catch (AssetValidationException ex)
        {
            return Result<AssetCapitalizationDto>.Failure(ex.Code, ex.Message);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Result<AssetCapitalizationDto>.Failure(ex.Code, ex.Message);
        }
    }

    private static decimal Round4(decimal value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);
}
