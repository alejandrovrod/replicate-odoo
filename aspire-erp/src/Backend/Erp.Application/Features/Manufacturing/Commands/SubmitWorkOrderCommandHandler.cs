using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Manufacturing.Commands;

/// <summary>
/// Executes <see cref="SubmitWorkOrderCommand"/>: Draft -&gt; Submitted (Task 9.3 workflow) after
/// the acceptance gate (BOM exists + <c>IsActive</c> + <c>IsDefault</c>). Any other current state
/// fails with <c>invalid_status_transition</c>, which the API maps to a 409. No stock and no GL
/// impact. A submit-time availability pre-check is deliberately deferred: the transfer (9.4)
/// and completion (9.4) postings enforce <c>InsufficientStockException</c> via FIFO consumption,
/// which is the hard guard that matters (spec MF-06).
/// </summary>
public sealed class SubmitWorkOrderCommandHandler
    : ICommandHandler<SubmitWorkOrderCommand, Result<WorkOrderDto>>
{
    private readonly IManufacturingRepository _manufacturing;

    public SubmitWorkOrderCommandHandler(IManufacturingRepository manufacturing)
    {
        _manufacturing = manufacturing;
    }

    public async Task<Result<WorkOrderDto>> HandleAsync(
        SubmitWorkOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var order = await _manufacturing.GetWorkOrderByIdAsync(command.WorkOrderId, cancellationToken)
                ?? throw new ManufacturingValidationException(
                    ManufacturingErrorCodes.WorkOrderNotFound,
                    $"Work order '{command.WorkOrderId}' was not found in this tenant.");

            if (order.CompanyId != command.CompanyId)
            {
                throw new ManufacturingValidationException(
                    ManufacturingErrorCodes.WorkOrderNotFound,
                    $"Work order '{order.OrderNumber}' does not belong to company '{command.CompanyId}'.");
            }

            var bom = await _manufacturing.GetBomByIdAsync(order.BomId, cancellationToken)
                ?? throw new ManufacturingValidationException(
                    ManufacturingErrorCodes.BomNotFound,
                    $"BOM '{order.BomId}' referenced by work order '{order.OrderNumber}' was not found in this tenant.");

            if (!bom.IsActive)
            {
                throw new ManufacturingValidationException(
                    ManufacturingErrorCodes.InactiveBom,
                    $"BOM '{bom.BomNumber}' is inactive and cannot authorize production of work order '{order.OrderNumber}'.");
            }

            if (!bom.IsDefault)
            {
                throw new ManufacturingValidationException(
                    ManufacturingErrorCodes.NonDefaultBom,
                    $"BOM '{bom.BomNumber}' is not the default recipe and cannot authorize production of work order '{order.OrderNumber}'.");
            }

            // Task 9.7 transitive closure: walk the WO BOM's components through THEIR default
            // active BOMs (only recipes that could authorize production matter). A chain leading
            // back to the finished item - or any repeat - closes a multi-level loop
            // (circular_reference, zero writes: the transition below never runs). A diamond
            // (shared sub-component, no loop) passes: the visited set is the CURRENT path, so a
            // component reached twice through different branches is legal.
            await EnsureNoTransitiveCycleAsync(order.ProductionItemId, bom, cancellationToken);

            order.Submit();
            await _manufacturing.UpdateWorkOrderAsync(order, cancellationToken);

            return Result<WorkOrderDto>.Success(WorkOrderDto.Build(order));
        }
        catch (ManufacturingValidationException ex)
        {
            return Result<WorkOrderDto>.Failure(ex.Code, ex.Message);
        }
        catch (ConcurrencyConflictException ex)
        {
            // Spec MF-06: the order changed under our feet (RowVersion mismatch on save).
            return Result<WorkOrderDto>.Failure(ex.Code, ex.Message);
        }
    }

    /// <summary>
    /// Depth-first walk over default active BOMs starting at the work order's recipe. Each frame
    /// carries its own ancestor chain (root finished item first), and every component is checked
    /// through <see cref="BomValidator.EnsureNoCycle"/> - the Task 9.2 plug-in point, same
    /// ancestor-id-list shape as the Account/Warehouse tree walks. Iterative (explicit stack),
    /// so a deep recipe ladder cannot overflow the call stack.
    /// </summary>
    private async Task EnsureNoTransitiveCycleAsync(
        Guid rootFinishedItemId,
        BillOfMaterials rootBom,
        CancellationToken cancellationToken)
    {
        var stack = new Stack<(BillOfMaterials Bom, List<Guid> Ancestors)>();
        stack.Push((rootBom, new List<Guid> { rootFinishedItemId }));

        while (stack.Count > 0)
        {
            var (bom, ancestors) = stack.Pop();

            foreach (var line in bom.Items)
            {
                BomValidator.EnsureNoCycle(line.ItemId, ancestors);

                var child = await _manufacturing.GetDefaultActiveBomByItemIdAsync(line.ItemId, cancellationToken);
                if (child is not null)
                {
                    var childAncestors = new List<Guid>(ancestors.Count + 1);
                    childAncestors.AddRange(ancestors);
                    childAncestors.Add(line.ItemId);
                    stack.Push((child, childAncestors));
                }
            }
        }
    }
}
