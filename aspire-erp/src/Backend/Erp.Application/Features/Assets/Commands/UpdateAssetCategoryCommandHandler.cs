using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Assets.Commands;

public sealed class UpdateAssetCategoryCommandHandler
    : ICommandHandler<UpdateAssetCategoryCommand, Result<AssetCategoryDto>>
{
    private readonly IAssetsRepository _assets;
    private readonly IAccountRepository _accounts;

    public UpdateAssetCategoryCommandHandler(
        IAssetsRepository assets,
        IAccountRepository accounts)
    {
        _assets = assets;
        _accounts = accounts;
    }

    public async Task<Result<AssetCategoryDto>> HandleAsync(
        UpdateAssetCategoryCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var category = await _assets.GetCategoryByIdAsync(command.Id, cancellationToken)
                ?? throw new AssetValidationException(
                    AssetErrorCodes.CategoryNotFound,
                    $"Asset category '{command.Id}' was not found.");

            if (category.RowVersion is null || !category.RowVersion.AsSpan().SequenceEqual(command.RowVersion))
            {
                throw new ConcurrencyConflictException(nameof(AssetCategory), category.Id);
            }

            AssetValidator.EnsureValidCategoryName(command.CategoryName);

            await AssetAccountGuards.RequirePostableAccountAsync(
                _accounts, command.FixedAssetAccountId, category.CompanyId, "fixed asset account", cancellationToken);
            await AssetAccountGuards.RequirePostableAccountAsync(
                _accounts, command.AccumulatedDepreciationAccountId, category.CompanyId, "accumulated depreciation account", cancellationToken);
            await AssetAccountGuards.RequirePostableAccountAsync(
                _accounts, command.DepreciationExpenseAccountId, category.CompanyId, "depreciation expense account", cancellationToken);

            if (command.CwipAccountId.HasValue)
            {
                await AssetAccountGuards.RequirePostableAccountAsync(
                    _accounts, command.CwipAccountId.Value, category.CompanyId, "CWIP account", cancellationToken);
            }

            if (command.GainOnDisposalAccountId.HasValue)
            {
                await AssetAccountGuards.RequirePostableAccountAsync(
                    _accounts, command.GainOnDisposalAccountId.Value, category.CompanyId, "gain on disposal account", cancellationToken);
            }

            if (command.LossOnDisposalAccountId.HasValue)
            {
                await AssetAccountGuards.RequirePostableAccountAsync(
                    _accounts, command.LossOnDisposalAccountId.Value, category.CompanyId, "loss on disposal account", cancellationToken);
            }

            category.CategoryName = command.CategoryName.Trim();
            category.FixedAssetAccountId = command.FixedAssetAccountId;
            category.AccumulatedDepreciationAccountId = command.AccumulatedDepreciationAccountId;
            category.DepreciationExpenseAccountId = command.DepreciationExpenseAccountId;
            category.CwipAccountId = command.CwipAccountId;
            category.GainOnDisposalAccountId = command.GainOnDisposalAccountId;
            category.LossOnDisposalAccountId = command.LossOnDisposalAccountId;
            category.IsNonDepreciable = command.IsNonDepreciable;
            category.IsActive = command.IsActive;

            await _assets.UpdateCategoryAsync(category, command.RowVersion, cancellationToken);

            return Result<AssetCategoryDto>.Success(AssetCategoryDto.Build(category));
        }
        catch (AssetValidationException ex)
        {
            return Result<AssetCategoryDto>.Failure(ex.Code, ex.Message);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Result<AssetCategoryDto>.Failure("concurrency_conflict", ex.Message);
        }
    }
}
