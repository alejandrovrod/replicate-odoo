using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Items.Commands;

public sealed class UpdateItemCommandHandler : ICommandHandler<UpdateItemCommand, Result<ItemDto>>
{
    private readonly IItemRepository _repository;
    private readonly ICustomFieldValidator? _customFields;

    public UpdateItemCommandHandler(IItemRepository repository, ICustomFieldValidator? customFields = null)
    {
        _repository = repository;
        _customFields = customFields;
    }

    public async Task<Result<ItemDto>> HandleAsync(UpdateItemCommand request, CancellationToken cancellationToken = default)
    {
        var item = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (item is null)
        {
            return Result<ItemDto>.Failure("item_not_found", "Item not found.");
        }

        // Concurrency check
        if (!item.RowVersion.SequenceEqual(request.RowVersion))
        {
            return Result<ItemDto>.Failure(
                "concurrency_conflict",
                "The item was modified by another user.");
        }

        if (item.ItemCode != request.Code)
        {
            // If changing code, check uniqueness within tenant
            var exists = await _repository.ExistsSkuAsync(request.Code, cancellationToken);
            if (exists)
            {
                return Result<ItemDto>.Failure(
                    StockErrorCodes.DuplicateItemCode,
                    "An item with this code already exists.");
            }
        }

        item.ItemCode = request.Code;
        item.ItemName = request.Name;
        item.ValuationMethod = request.ValuationMethod;
        item.StockUomId = request.BaseUOMId;
        item.IncomeAccountId = request.IncomeAccountId;
        item.ExpenseAccountId = request.ExpenseAccountId;
        item.IsActive = request.IsActive;

        // Batch-and-serial Fase 3: traceability flags are master data (no movement impact
        // on change; in-flight vouchers already validated against the previous flags).
        item.HasBatchNo = request.HasBatchNo;
        item.HasSerialNo = request.HasSerialNo;

        // Extensibility Fase 1: null means "not sent" (keep); an explicit value is validated.
        if (request.CustomData is not null)
        {
            if (_customFields is not null)
            {
                var customError = await _customFields.ValidateAsync("Item", request.CustomData, cancellationToken);
                if (customError is not null)
                {
                    return Result<ItemDto>.Failure(customError.Code, customError.Message);
                }
            }

            item.CustomData = request.CustomData;
        }

        await _repository.UpdateAsync(item, cancellationToken);

        return Result<ItemDto>.Success(ItemDto.From(item, []));
    }
}
