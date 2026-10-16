using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using Erp.Domain.Services;

namespace Erp.Application.Features.Selling.Commands;

/// <summary>
/// Executes <see cref="SubmitSalesOrderCommand"/>: Draft -&gt; Submitted (Task 5.2 workflow),
/// guarded by the plan.md §2 credit gate - the FIRST thing that runs against the customer, so a
/// breach leaves the order in Draft with zero writes.
/// </summary>
/// <remarks>
/// Any state other than Draft fails with <c>invalid_status_transition</c> (409); a breach fails
/// with <c>credit_limit_exceeded</c> (409) carrying the evaluator's message. A RowVersion race on
/// the save is <c>concurrency_conflict</c> (409).
/// </remarks>
public sealed class SubmitSalesOrderCommandHandler
    : ICommandHandler<SubmitSalesOrderCommand, Result<SalesOrderDto>>
{
    private readonly ICustomerRepository _customers;
    private readonly IItemRepository _items;
    private readonly ISalesOrderRepository _salesOrders;
    private readonly IEnumerable<IDocumentLifecycleHook<SalesOrder>>? _hooks;

    public SubmitSalesOrderCommandHandler(
        ICustomerRepository customers,
        IItemRepository items,
        ISalesOrderRepository salesOrders,
        IEnumerable<IDocumentLifecycleHook<SalesOrder>>? hooks = null)
    {
        _customers = customers;
        _items = items;
        _salesOrders = salesOrders;
        _hooks = hooks;
    }

    public async Task<Result<SalesOrderDto>> HandleAsync(
        SubmitSalesOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var order = await _salesOrders.GetOrderByIdAsync(command.SalesOrderId, cancellationToken)
                ?? throw new SalesValidationException(
                    SellingErrorCodes.SalesOrderNotFound,
                    $"Sales order '{command.SalesOrderId}' was not found in this tenant.");

            if (order.CompanyId != command.CompanyId)
            {
                throw new SalesValidationException(
                    SellingErrorCodes.SalesOrderNotFound,
                    $"Sales order '{order.OrderNumber}' does not belong to company '{command.CompanyId}'.");
            }

            SalesValidator.EnsureSubmittable(order.Status);

            // Workflow Fase 5 (gap D2): operational gate before the transition - a gated
            // document in a non-submittable state fails here with zero writes.
            await DocumentLifecycleInvoker.RunBeforeSubmitAsync(_hooks, order, cancellationToken);

            var customer = await _customers.GetByIdAsync(order.CustomerId, cancellationToken)
                ?? throw new SalesValidationException(
                    SellingErrorCodes.CustomerNotFound,
                    $"Customer '{order.CustomerId}' was not found in this tenant.");

            // spec SL-02 / plan.md §2: the credit gate runs BEFORE the transition - a breach
            // throws CreditLimitExceededException and the order stays in Draft.
            CreditControlEvaluator.ValidateCreditExposure(customer, order.GrandTotal);

            order.Status = SalesOrderStatus.Submitted;
            await _salesOrders.UpdateOrderAsync(order, cancellationToken);

            await DocumentLifecycleInvoker.RunAfterSubmitAsync(_hooks, order, cancellationToken);

            var items = await LoadItemsAsync(order, cancellationToken);

            return Result<SalesOrderDto>.Success(SalesOrderDto.Build(order, customer, items));
        }
        catch (SalesValidationException ex)
        {
            return Result<SalesOrderDto>.Failure(ex.Code, ex.Message);
        }
        catch (WorkflowValidationException ex)
        {
            // Workflow Fase 5: the guard's own stable code travels verbatim
            // (workflow_submit_not_allowed); the order stays Draft with zero writes.
            return Result<SalesOrderDto>.Failure(ex.Code, ex.Message);
        }
        catch (LifecycleHookRejectedException ex)
        {
            return Result<SalesOrderDto>.Failure(ex.Code, ex.Message);
        }
        catch (CreditLimitExceededException ex)
        {
            return Result<SalesOrderDto>.Failure(ex.Code, ex.Message);
        }
        catch (ConcurrencyConflictException ex)
        {
            // The order changed under our feet (RowVersion mismatch on save).
            return Result<SalesOrderDto>.Failure(ex.Code, ex.Message);
        }
    }

    private async Task<Dictionary<Guid, Item>> LoadItemsAsync(SalesOrder order, CancellationToken cancellationToken)
    {
        var ids = new List<Guid>(order.Lines.Count);
        foreach (var line in order.Lines)
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
