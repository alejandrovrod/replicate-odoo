using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Buying.Commands;

/// <summary>
/// Executes <see cref="UpdatePurchaseOrderCommand"/>: validates the order is in Draft state,
/// clears and replaces the lines, updates the header fields, and saves.
/// </summary>
public sealed class UpdatePurchaseOrderCommandHandler
    : ICommandHandler<UpdatePurchaseOrderCommand, Result<PurchaseOrderDto>>
{
    private readonly ISupplierRepository _suppliers;
    private readonly IItemRepository _items;
    private readonly IPurchaseRepository _purchases;

    public UpdatePurchaseOrderCommandHandler(
        ISupplierRepository suppliers,
        IItemRepository items,
        IPurchaseRepository purchases)
    {
        _suppliers = suppliers;
        _items = items;
        _purchases = purchases;
    }

    public async Task<Result<PurchaseOrderDto>> HandleAsync(
        UpdatePurchaseOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            PurchaseValidator.EnsureHasLines(command.Items);

            var order = await _purchases.GetOrderByIdAsync(command.PurchaseOrderId, cancellationToken)
                ?? throw new PurchaseValidationException(
                    PurchaseErrorCodes.PurchaseOrderNotFound,
                    $"Purchase order '{command.PurchaseOrderId}' was not found in this tenant.");

            if (order.CompanyId != command.CompanyId)
            {
                throw new PurchaseValidationException(
                    PurchaseErrorCodes.PurchaseOrderNotFound,
                    $"Purchase order '{order.OrderNumber}' does not belong to company '{command.CompanyId}'.");
            }

            PurchaseValidator.EnsureDraft(order.Status);

            var supplier = await _suppliers.GetByIdAsync(command.SupplierId, cancellationToken)
                ?? throw new PurchaseValidationException(
                    PurchaseErrorCodes.SupplierNotFound,
                    $"Supplier '{command.SupplierId}' was not found in this tenant.");

            if (!supplier.IsActive)
            {
                throw new PurchaseValidationException(
                    PurchaseErrorCodes.SupplierInactive,
                    $"Supplier '{supplier.Code}' is inactive and cannot be used in a purchase order.");
            }

            var items = await LoadItemsAsync(command.Items!, cancellationToken);

            decimal netTotal = 0;
            foreach (var item in command.Items!)
            {
                netTotal += item.Quantity * item.Rate;
            }

            order.SupplierId = supplier.Id;
            order.TransactionDate = command.TransactionDate;
            order.ScheduleDate = command.ScheduleDate;
            order.NetTotal = netTotal;
            order.GrandTotal = netTotal;

            order.Items.Clear();
            var newItems = BuildItems(command.Items!, items);
            foreach (var ni in newItems)
            {
                order.Items.Add(ni);
            }

            await _purchases.UpdateOrderAsync(order, cancellationToken);

            return Result<PurchaseOrderDto>.Success(PurchaseOrderDto.Build(order, supplier, items));
        }
        catch (PurchaseValidationException ex)
        {
            return Result<PurchaseOrderDto>.Failure(ex.Code, ex.Message);
        }
        catch (StockValidationException ex)
        {
            return Result<PurchaseOrderDto>.Failure(ex.Code, ex.Message);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Result<PurchaseOrderDto>.Failure(ex.Code, ex.Message);
        }
    }

    private async Task<Dictionary<Guid, Item>> LoadItemsAsync(
        IReadOnlyList<UpdatePurchaseOrderItem> items,
        CancellationToken cancellationToken)
    {
        var ids = new List<Guid>(items.Count);
        foreach (var item in items)
        {
            if (item.ItemId == Guid.Empty)
            {
                throw new StockValidationException(
                    StockErrorCodes.ItemNotFound, "A line references an empty ItemId.");
            }

            if (!ids.Contains(item.ItemId))
            {
                ids.Add(item.ItemId);
            }
        }

        var found = await _items.GetByIdsAsync(ids, cancellationToken);
        var byId = new Dictionary<Guid, Item>(found.Count);
        foreach (var item in found)
        {
            byId[item.Id] = item;
        }

        foreach (var id in ids)
        {
            if (!byId.ContainsKey(id))
            {
                throw new StockValidationException(
                    StockErrorCodes.ItemNotFound,
                    $"Item '{id}' was not found in this tenant.");
            }
        }

        return byId;
    }

    private static List<PurchaseOrderItem> BuildItems(
        IReadOnlyList<UpdatePurchaseOrderItem> items,
        IReadOnlyDictionary<Guid, Item> itemMap)
    {
        var result = new List<PurchaseOrderItem>(items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            PurchaseValidator.EnsureValidLine(item.Quantity, item.Rate);
            _ = itemMap[item.ItemId];

            result.Add(new PurchaseOrderItem
            {
                Id = Guid.NewGuid(),
                ItemId = item.ItemId,
                Quantity = item.Quantity,
                Rate = item.Rate,
                Amount = item.Quantity * item.Rate,
                LineNumber = i + 1,
            });
        }

        return result;
    }
}
