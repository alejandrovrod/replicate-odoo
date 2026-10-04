using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Services;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Manufacturing.Commands;

/// <summary>
/// Executes <see cref="TransferMaterialsToWipCommand"/> by orchestrating
/// <see cref="IStockPostingService"/>: the transfer branch already posts Dr target-account /
/// Cr source-account at FIFO value with range locks, DoubleEntryGuard, gapless numbering and
/// the frozen-period gate - with Stores -&gt; WIP warehouses linked to the 1310/1320 accounts
/// that pair IS the MF-02 voucher, so no stock-engine extension was needed. The order
/// transition (Submitted -&gt; InProcess) commits in the SAME ambient transaction as the posting
/// (shared scoped DbContext), and only after the posting succeeds.
/// </summary>
public sealed class TransferMaterialsToWipCommandHandler
    : ICommandHandler<TransferMaterialsToWipCommand, Result<StockEntryPostingDto>>
{
    private readonly IManufacturingRepository _manufacturing;
    private readonly IStockPostingService _posting;

    public TransferMaterialsToWipCommandHandler(
        IManufacturingRepository manufacturing,
        IStockPostingService posting)
    {
        _manufacturing = manufacturing;
        _posting = posting;
    }

    public async Task<Result<StockEntryPostingDto>> HandleAsync(
        TransferMaterialsToWipCommand command,
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

            if (order.Status != WorkOrderStatus.Submitted)
            {
                throw new ManufacturingValidationException(
                    ManufacturingErrorCodes.InvalidStatusTransition,
                    $"Materials can be transferred to WIP only from a Submitted work order; order '{order.OrderNumber}' is '{order.Status}'.");
            }

            var bom = await _manufacturing.GetBomByIdAsync(order.BomId, cancellationToken)
                ?? throw new ManufacturingValidationException(
                    ManufacturingErrorCodes.BomNotFound,
                    $"BOM '{order.BomId}' referenced by work order '{order.OrderNumber}' was not found in this tenant.");

            if (bom.Quantity <= 0)
            {
                throw new ManufacturingValidationException(
                    ManufacturingErrorCodes.InvalidBomQuantity,
                    $"BOM '{bom.BomNumber}' has no valid yield quantity for scaling the transfer lines.");
            }

            var postingDate = command.PostingDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var lines = BuildTransferLines(bom, order.QuantityToProduce);

            var posting = await _manufacturing.ExecuteInTransactionAsync(async token =>
            {
                var result = await _posting.PostAsync(
                    new StockPostingRequest(
                        order.CompanyId,
                        StockEntryType.MaterialTransfer,
                        postingDate,
                        order.SourceWarehouseId,
                        order.WipWarehouseId,
                        lines),
                    token);

                order.StartProduction();
                order.ActualStartDate = postingDate;

                // Task 9.6 linkage (option (a)): remember the MF-02 voucher so the cancellation
                // handler can reverse exactly it. Same ambient transaction - link and posting
                // commit together or not at all.
                order.TransferStockEntryId = result.Entry.Id;
                await _manufacturing.UpdateWorkOrderAsync(order, token);

                return result;
            }, cancellationToken);

            return Result<StockEntryPostingDto>.Success(posting);
        }
        catch (ManufacturingValidationException ex)
        {
            return Result<StockEntryPostingDto>.Failure(ex.Code, ex.Message);
        }
        catch (StockValidationException ex)
        {
            return Result<StockEntryPostingDto>.Failure(ex.Code, ex.Message);
        }
        catch (InsufficientStockException ex)
        {
            // Spec MF-06: Stores cannot cover the transfer - the order stays Submitted, untouched.
            return Result<StockEntryPostingDto>.Failure(ex.Code, ex.Message);
        }
        catch (FiscalPeriodLockedException ex)
        {
            return Result<StockEntryPostingDto>.Failure(ex.Code, ex.Message);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Result<StockEntryPostingDto>.Failure(ex.Code, ex.Message);
        }
        catch (NotSupportedException ex)
        {
            // Only FIFO is implemented - report it as an explicit client-visible failure.
            return Result<StockEntryPostingDto>.Failure("unsupported_valuation_method", ex.Message);
        }
    }

    /// <summary>
    /// One transfer line per BOM component, scaled to the authorized quantity: a BOM yielding
    /// <c>bom.Quantity</c> units consumes <c>item.Quantity</c> each, so the order consumes
    /// <c>item.Quantity x orderQty / bom.Quantity</c>. Rates stay null - the FIFO engine values them.
    /// </summary>
    private static IReadOnlyList<StockPostingLine> BuildTransferLines(BillOfMaterials bom, decimal orderQuantity)
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
