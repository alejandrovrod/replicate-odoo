using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Services;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Manufacturing.Commands;

/// <summary>
/// Executes <see cref="CancelWorkOrderCommand"/> (Task 9.6, spec MF-05). Allowed from Submitted
/// and InProcess ONLY - Draft (nothing issued yet, but cancellation before submission is a
/// workflow no-op by design), Completed and Cancelled all fail with
/// <c>invalid_status_transition</c>, which the API maps to a 409. A Completed order (or any
/// produced quantity) is NEVER reversed: its FIFO layers stay consumed.
/// </summary>
/// <remarks>
/// The reversal reuses <see cref="IStockPostingService"/> - it is never a hand-built voucher:
/// the compensating <c>MaterialTransfer</c> (WIP -&gt; Stores) consumes the WIP layers at FIFO
/// cost (the layers the MF-02 transfer laid down, when WIP is otherwise untouched) and posts
/// the swapped GL mirror (Dr 1310 / Cr 1320) through the same range locks, frozen-period gate,
/// DoubleEntryGuard and gapless MT numbering as every other transfer. The link to the original
/// voucher is <see cref="WorkOrder.TransferStockEntryId"/> (option (a)): one order holds at
/// most one transfer voucher because partial multi-voucher staging stays deferred. The original
/// voucher keeps its number and history (a NEW compensating voucher reverses it); the order
/// lands in Cancelled with <c>ActualEndDate</c> stamped. Everything - compensation, link and
/// transition - commits in ONE ambient transaction, so a rejection writes ZERO rows.
/// </remarks>
public sealed class CancelWorkOrderCommandHandler
    : ICommandHandler<CancelWorkOrderCommand, Result<WorkOrderDto>>
{
    private readonly IManufacturingRepository _manufacturing;
    private readonly IStockPostingService _posting;

    public CancelWorkOrderCommandHandler(
        IManufacturingRepository manufacturing,
        IStockPostingService posting)
    {
        _manufacturing = manufacturing;
        _posting = posting;
    }

    public async Task<Result<WorkOrderDto>> HandleAsync(
        CancelWorkOrderCommand command,
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

            if (order.Status is not WorkOrderStatus.Submitted and not WorkOrderStatus.InProcess
                || order.ProducedQuantity > 0)
            {
                throw new ManufacturingValidationException(
                    ManufacturingErrorCodes.InvalidStatusTransition,
                    $"Only a Submitted or InProcess work order with no production can be cancelled; order '{order.OrderNumber}' is '{order.Status}'.");
            }

            // No transfer ever posted: a pure status transition (no stock, no GL impact).
            if (order.TransferStockEntryId is null)
            {
                order.Cancel();
                order.ActualEndDate = command.PostingDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
                await _manufacturing.UpdateWorkOrderAsync(order, cancellationToken);

                return Result<WorkOrderDto>.Success(WorkOrderDto.Build(order));
            }

            var bom = await _manufacturing.GetBomByIdAsync(order.BomId, cancellationToken)
                ?? throw new ManufacturingValidationException(
                    ManufacturingErrorCodes.BomNotFound,
                    $"BOM '{order.BomId}' referenced by work order '{order.OrderNumber}' was not found in this tenant.");

            if (bom.Quantity <= 0)
            {
                throw new ManufacturingValidationException(
                    ManufacturingErrorCodes.InvalidBomQuantity,
                    $"BOM '{bom.BomNumber}' has no valid yield quantity for scaling the reversal lines.");
            }

            var postingDate = command.PostingDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var lines = BuildReversalLines(bom, order.QuantityToProduce);

            var cancelled = await _manufacturing.ExecuteInTransactionAsync(async token =>
            {
                // WIP -&gt; Stores: the FIFO engine consumes the transferred layers and the
                // transfer branch posts the swapped mirror (Dr Stores / Cr WIP) by construction.
                await _posting.PostAsync(
                    new StockPostingRequest(
                        order.CompanyId,
                        StockEntryType.MaterialTransfer,
                        postingDate,
                        order.WipWarehouseId,
                        order.SourceWarehouseId,
                        lines),
                    token);

                order.Cancel();
                order.ActualEndDate = postingDate;
                await _manufacturing.UpdateWorkOrderAsync(order, token);

                return order;
            }, cancellationToken);

            return Result<WorkOrderDto>.Success(WorkOrderDto.Build(cancelled));
        }
        catch (ManufacturingValidationException ex)
        {
            return Result<WorkOrderDto>.Failure(ex.Code, ex.Message);
        }
        catch (StockValidationException ex)
        {
            return Result<WorkOrderDto>.Failure(ex.Code, ex.Message);
        }
        catch (InsufficientStockException ex)
        {
            // WIP no longer covers the reversal (something consumed it meanwhile) - the order
            // stays InProcess, untouched.
            return Result<WorkOrderDto>.Failure(ex.Code, ex.Message);
        }
        catch (FiscalPeriodLockedException ex)
        {
            return Result<WorkOrderDto>.Failure(ex.Code, ex.Message);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Result<WorkOrderDto>.Failure(ex.Code, ex.Message);
        }
        catch (NotSupportedException ex)
        {
            // Only FIFO is implemented - report it as an explicit client-visible failure.
            return Result<WorkOrderDto>.Failure("unsupported_valuation_method", ex.Message);
        }
    }

    /// <summary>
    /// One reversal line per BOM component, scaled exactly like the MF-02 transfer
    /// (BOM x authorized quantity) - the quantities the compensating voucher moves back.
    /// Rates stay null - the FIFO engine values them from the WIP layers.
    /// </summary>
    private static IReadOnlyList<StockPostingLine> BuildReversalLines(BillOfMaterials bom, decimal orderQuantity)
    {
        var lines = new List<StockPostingLine>(bom.Items.Count);
        foreach (var item in bom.Items.OrderBy(i => i.ItemId))
        {
            var required = Math.Round(item.Quantity * orderQuantity / bom.Quantity, 4, MidpointRounding.AwayFromZero);
            lines.Add(new StockPostingLine(item.ItemId, required, Rate: null));
        }

        return lines;
    }
}
