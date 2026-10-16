using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Items.Commands;

/// <summary>
/// Executes <see cref="CreateItemCommand"/>: pure Domain validation (ItemValidator), the FK
/// existence checks that need data access (Base UOM / income / expense accounts), and the
/// duplicate-SKU-per-tenant rule that Task 3.1's acceptance criterion demands.
/// </summary>
public sealed class CreateItemCommandHandler : ICommandHandler<CreateItemCommand, Result<ItemDto>>
{
    private readonly IItemRepository _items;
    private readonly IUomRepository _uoms;
    private readonly IAccountRepository _accounts;
    private readonly ICustomFieldValidator? _customFields;

    public CreateItemCommandHandler(IItemRepository items, IUomRepository uoms, IAccountRepository accounts, ICustomFieldValidator? customFields = null)
    {
        _items = items;
        _uoms = uoms;
        _accounts = accounts;
        _customFields = customFields;
    }

    public async Task<Result<ItemDto>> HandleAsync(CreateItemCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            ItemValidator.EnsureValidFields(
                command.Code,
                command.Name,
                command.ValuationMethod,
                command.BaseUOMId);

            var uom = await _uoms.GetByIdAsync(command.BaseUOMId, cancellationToken);
            if (uom is null)
            {
                throw new StockValidationException(
                    StockErrorCodes.BaseUomRequired,
                    $"Base UOM '{command.BaseUOMId}' was not found in this tenant.");
            }

            await EnsureAccountExistsAsync(command.IncomeAccountId, "income", cancellationToken);
            await EnsureAccountExistsAsync(command.ExpenseAccountId, "expense", cancellationToken);

            var code = command.Code.Trim();

            if (await _items.ExistsSkuAsync(code, cancellationToken))
            {
                throw new StockValidationException(
                    StockErrorCodes.DuplicateItemCode,
                    $"Item code '{code}' already exists in this tenant.");
            }

            // Extensibility Fase 1 (gap D2): validated BEFORE the insert.
            if (_customFields is not null)
            {
                var customError = await _customFields.ValidateAsync("Item", command.CustomData, cancellationToken);
                if (customError is not null)
                {
                    throw new StockValidationException(customError.Code, customError.Message);
                }
            }

            var item = new Item
            {
                // Id is generated here so tests can inspect the entity before it is persisted.
                Id = Guid.NewGuid(),
                ItemCode = code,
                ItemName = command.Name.Trim(),
                ValuationMethod = command.ValuationMethod,
                StockUomId = command.BaseUOMId,
                IncomeAccountId = command.IncomeAccountId,
                ExpenseAccountId = command.ExpenseAccountId,
                IsActive = command.IsActive,
                CustomData = command.CustomData,
                HasBatchNo = command.HasBatchNo,
                HasSerialNo = command.HasSerialNo,

                // TenantId is intentionally NOT set: AppDbContext stamps CurrentTenantId on insert
                // and throws when no tenant context exists (Constitution Article II.4, fail closed).
            };

            await _items.AddAsync(item, cancellationToken);

            return Result<ItemDto>.Success(ItemDto.From(item));
        }
        catch (StockValidationException ex)
        {
            return Result<ItemDto>.Failure(ex.Code, ex.Message);
        }
    }

    private async Task EnsureAccountExistsAsync(Guid? accountId, string role, CancellationToken cancellationToken)
    {
        if (accountId is not { } id || id == Guid.Empty)
        {
            return;
        }

        if (await _accounts.GetByIdAsync(id, cancellationToken) is null)
        {
            throw new StockValidationException(
                StockErrorCodes.InvalidGlAccount,
                $"The {role} account '{id}' does not exist in this tenant.");
        }
    }
}
