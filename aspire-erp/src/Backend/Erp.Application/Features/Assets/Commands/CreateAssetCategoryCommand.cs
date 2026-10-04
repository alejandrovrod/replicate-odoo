using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Assets.Commands;

/// <summary>
/// Creates one asset category with its GL account template links (Task 10.1). Gain/loss disposal
/// accounts are optional here (required later at disposal time, Block B); CWIP is optional here
/// but required at capitalization time (Task 10.2 boundary).
/// </summary>
public sealed record CreateAssetCategoryCommand(
    Guid CompanyId,
    string CategoryName,
    Guid FixedAssetAccountId,
    Guid AccumulatedDepreciationAccountId,
    Guid DepreciationExpenseAccountId,
    Guid? CwipAccountId = null,
    Guid? GainOnDisposalAccountId = null,
    Guid? LossOnDisposalAccountId = null,
    bool IsNonDepreciable = false) : ICommand<Result<AssetCategoryDto>>;
