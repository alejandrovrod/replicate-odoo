using Erp.Application.DTOs;
using Erp.Domain.Exceptions;

namespace Erp.Application.Services;

/// <summary>One line of a delivery note posting request (qty in the item's Base UOM).</summary>
public sealed record DeliveryNotePostingLine(Guid SalesOrderItemId, Guid ItemId, decimal Qty);

/// <summary>
/// Everything the selling posting engine needs to atomically post one delivery note
/// (Task 5.2b / Amendment A1). There is no draft state: the request IS the posting.
/// </summary>
public sealed record DeliveryNotePostingRequest(
    Guid CompanyId,
    Guid SalesOrderId,
    Guid WarehouseId,
    DateOnly PostingDate,
    IReadOnlyList<DeliveryNotePostingLine> Lines);

/// <summary>
/// Application-level orchestration of the delivery posting (Task 5.2b): resolves the company,
/// order, warehouse, items and GL accounts through repositories, applies the SL-04
/// non-overdelivery guard, values the shipment with the FIFO engine, writes StockLedgerEntry +
/// balanced GLEntry rows, assigns the gapless DN voucher and advances the sales order - all
/// inside ONE database transaction. No EF Core here: repositories are Domain contracts only
/// (Constitution Article I.3).
/// </summary>
public interface ISalesPostingService
{
    /// <summary>
    /// Posts a delivery note: -Kardex at FIFO cost, Dr Company.CogsAccountCode / Cr warehouse
    /// stock account, gapless DN-YYYY-NNNNN voucher and the order's
    /// Submitted/PartiallyDelivered -&gt; PartiallyDelivered/Completed transition with the
    /// recomputed DeliveredQuantity/DeliveredPercentage.
    /// </summary>
    /// <exception cref="SalesValidationException">A selling/domain invariant was violated (4xx).</exception>
    /// <exception cref="StockValidationException">A stock concept was violated (company/warehouse/item, 4xx).</exception>
    /// <exception cref="OverdeliveryNotAllowedException">spec SL-04: the line ships more than the order owes (400).</exception>
    /// <exception cref="InsufficientStockException">FIFO layers ran dry and negative stock is forbidden (4xx).</exception>
    /// <exception cref="FiscalPeriodLockedException">PostingDate is inside the frozen fiscal period (409).</exception>
    /// <exception cref="ConcurrencyConflictException">The order changed between load and save (409).</exception>
    /// <exception cref="NotSupportedException">The item uses a valuation method other than FIFO.</exception>
    /// <exception cref="StockPostingConfigurationException">GL/company configuration is missing (500).</exception>
    Task<DeliveryNotePostingDto> PostDeliveryNoteAsync(
        DeliveryNotePostingRequest request,
        CancellationToken cancellationToken = default);
}
