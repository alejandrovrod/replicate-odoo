using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Assets.Commands;

/// <summary>
/// Generates (or regenerates, when still untouched) the straight-line depreciation schedule of ONE
/// asset (asset-capitalization module, task T4). Complements the automatic generation performed
/// when an asset is capitalized from a vendor bill or an opening entry: it lets an operator build
/// the calendar for a Draft/Submitted asset that entered without one.
/// </summary>
/// <remarks>
/// Idempotent by guard, not by key: an asset that already has schedule lines is rejected with
/// <c>asset_schedule_already_exists</c> so a replay cannot double the calendar. No General Ledger
/// rows are written here - the schedule is a plan, not a posting.
/// </remarks>
public sealed record GenerateAssetScheduleCommand(
    Guid CompanyId,
    Guid AssetId) : ICommand<Result<AssetCapitalizationDto>>;
