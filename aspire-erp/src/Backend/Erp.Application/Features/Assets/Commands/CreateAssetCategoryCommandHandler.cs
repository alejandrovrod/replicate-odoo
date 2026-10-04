using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Assets.Commands;

/// <summary>
/// Executes <see cref="CreateAssetCategoryCommand"/> (Task 10.1): resolves EVERY linked account
/// by id through the Constitution III.3 leaf-posting guard (exists + active + IsGroup == false +
/// company-owned) and persists the template. Gain/loss disposal accounts are validated the same
/// way WHEN SET (nullable - required later at disposal time, Block B). A rejected creation
/// writes zero rows (the guard failures throw before the first Add).
/// </summary>
public sealed class CreateAssetCategoryCommandHandler
    : ICommandHandler<CreateAssetCategoryCommand, Result<AssetCategoryDto>>
{
    private readonly ICompanyRepository _companies;
    private readonly IAccountRepository _accounts;
    private readonly IAssetsRepository _assets;

    public CreateAssetCategoryCommandHandler(
        ICompanyRepository companies,
        IAccountRepository accounts,
        IAssetsRepository assets)
    {
        _companies = companies;
        _accounts = accounts;
        _assets = assets;
    }

    public async Task<Result<AssetCategoryDto>> HandleAsync(
        CreateAssetCategoryCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            AssetValidator.EnsureValidCategoryName(command.CategoryName);

            var company = await _companies.GetByIdAsync(command.CompanyId, cancellationToken)
                ?? throw new AssetValidationException(
                    AssetErrorCodes.CompanyNotFound,
                    $"Company '{command.CompanyId}' was not found in this tenant.");

            await AssetAccountGuards.RequirePostableAccountAsync(
                _accounts, command.FixedAssetAccountId, company.Id, "fixed asset account", cancellationToken);
            await AssetAccountGuards.RequirePostableAccountAsync(
                _accounts, command.AccumulatedDepreciationAccountId, company.Id, "accumulated depreciation account", cancellationToken);
            await AssetAccountGuards.RequirePostableAccountAsync(
                _accounts, command.DepreciationExpenseAccountId, company.Id, "depreciation expense account", cancellationToken);

            if (command.CwipAccountId.HasValue)
            {
                await AssetAccountGuards.RequirePostableAccountAsync(
                    _accounts, command.CwipAccountId.Value, company.Id, "CWIP account", cancellationToken);
            }

            if (command.GainOnDisposalAccountId.HasValue)
            {
                await AssetAccountGuards.RequirePostableAccountAsync(
                    _accounts, command.GainOnDisposalAccountId.Value, company.Id, "gain on disposal account", cancellationToken);
            }

            if (command.LossOnDisposalAccountId.HasValue)
            {
                await AssetAccountGuards.RequirePostableAccountAsync(
                    _accounts, command.LossOnDisposalAccountId.Value, company.Id, "loss on disposal account", cancellationToken);
            }

            var category = new AssetCategory
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                CategoryName = command.CategoryName.Trim(),
                FixedAssetAccountId = command.FixedAssetAccountId,
                AccumulatedDepreciationAccountId = command.AccumulatedDepreciationAccountId,
                DepreciationExpenseAccountId = command.DepreciationExpenseAccountId,
                CwipAccountId = command.CwipAccountId,
                GainOnDisposalAccountId = command.GainOnDisposalAccountId,
                LossOnDisposalAccountId = command.LossOnDisposalAccountId,
                IsNonDepreciable = command.IsNonDepreciable,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            await _assets.AddCategoryAsync(category, cancellationToken);

            return Result<AssetCategoryDto>.Success(AssetCategoryDto.Build(category));
        }
        catch (AssetValidationException ex)
        {
            return Result<AssetCategoryDto>.Failure(ex.Code, ex.Message);
        }
    }
}
