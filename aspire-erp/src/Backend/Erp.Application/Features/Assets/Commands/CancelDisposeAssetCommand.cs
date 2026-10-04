using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Assets.Commands;

/// <summary>
/// Reverses an asset disposal (Task 10.6 / spec AS-05 reversal): undoes the disposal GL lines,
/// reopens cancelled schedule lines, restores AccumulatedDepreciation to its pre-disposal value,
/// clears DisposalDate and restores the asset to Capitalized (or FullyDepreciated).
/// </summary>
public sealed record CancelDisposeAssetCommand(
    Guid CompanyId,
    Guid AssetId,
    DateOnly? PostingDate = null,
    byte[]? RowVersion = null) : ICommand<Result<AssetDisposalReversalDto>>;