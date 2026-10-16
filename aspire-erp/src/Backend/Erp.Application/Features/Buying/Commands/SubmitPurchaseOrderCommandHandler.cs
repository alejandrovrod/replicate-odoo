using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Buying.Commands;

/// <summary>
/// Executes <see cref="SubmitPurchaseOrderCommand"/>: Draft -&gt; Submitted (Task 4.1 workflow).
/// Any other current state fails with <c>invalid_status_transition</c>, which the API maps to a
/// 409 - the order conflicts with the requested state. No stock and no GL impact.
/// </summary>
public sealed class SubmitPurchaseOrderCommandHandler
    : ICommandHandler<SubmitPurchaseOrderCommand, Result<PurchaseOrderDto>>
{
    private readonly ISupplierRepository _suppliers;
    private readonly IItemRepository _items;
    private readonly IPurchaseRepository _purchases;
    private readonly IEnumerable<IDocumentLifecycleHook<PurchaseOrder>>? _hooks;

    public SubmitPurchaseOrderCommandHandler(
        ISupplierRepository suppliers,
        IItemRepository items,
        IPurchaseRepository purchases,
        IEnumerable<IDocumentLifecycleHook<PurchaseOrder>>? hooks = null)
    {
        _suppliers = suppliers;
        _items = items;
        _purchases = purchases;
        _hooks = hooks;
    }

    public async Task<Result<PurchaseOrderDto>> HandleAsync(
        SubmitPurchaseOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
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

            PurchaseValidator.EnsureSubmittable(order.Status);

            // Workflow Fase 5 (gap D2): operational gate before the transition.
            await DocumentLifecycleInvoker.RunBeforeSubmitAsync(_hooks, order, cancellationToken);

            order.Status = PurchaseOrderStatus.Submitted;
            await _purchases.UpdateOrderAsync(order, cancellationToken);

            await DocumentLifecycleInvoker.RunAfterSubmitAsync(_hooks, order, cancellationToken);

            var supplier = await _suppliers.GetByIdAsync(order.SupplierId, cancellationToken)
                ?? throw new PurchaseValidationException(
                    PurchaseErrorCodes.SupplierNotFound,
                    $"Supplier '{order.SupplierId}' was not found in this tenant.");

            var items = await LoadItemsAsync(order, cancellationToken);

            return Result<PurchaseOrderDto>.Success(PurchaseOrderDto.Build(order, supplier, items));
        }
        catch (PurchaseValidationException ex)
        {
            return Result<PurchaseOrderDto>.Failure(ex.Code, ex.Message);
        }
        catch (WorkflowValidationException ex)
        {
            // Workflow Fase 5: the guard's own stable code travels verbatim.
            return Result<PurchaseOrderDto>.Failure(ex.Code, ex.Message);
        }
        catch (LifecycleHookRejectedException ex)
        {
            return Result<PurchaseOrderDto>.Failure(ex.Code, ex.Message);
        }
        catch (ConcurrencyConflictException ex)
        {
            // Spec BY-06: the order changed under our feet (RowVersion mismatch on save).
            return Result<PurchaseOrderDto>.Failure(ex.Code, ex.Message);
        }
    }

    private async Task<Dictionary<Guid, Item>> LoadItemsAsync(PurchaseOrder order, CancellationToken cancellationToken)
    {
        var ids = new List<Guid>(order.Items.Count);
        foreach (var line in order.Items)
        {
            if (!ids.Contains(line.ItemId))
            {
                ids.Add(line.ItemId);
            }
        }

        var found = await _items.GetByIdsAsync(ids, cancellationToken);
        var byId = new Dictionary<Guid, Item>(found.Count);
        foreach (var item in found)
        {
            byId[item.Id] = item;
        }

        return byId;
    }
}
