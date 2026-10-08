using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Assets.Commands;

public sealed record UpdateAssetCategoryCommand(
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
    byte[] RowVersion) : ICommand<Result<AssetCategoryDto>>;
