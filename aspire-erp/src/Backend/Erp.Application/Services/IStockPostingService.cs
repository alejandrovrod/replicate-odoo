using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;

namespace Erp.Application.Services;

/// <summary>One line of a stock posting request (qty in the item's Base UOM).</summary>
/// <param name="ItemId">Item that moves.</param>
/// <param name="Qty">Quantity; must be strictly positive.</param>
/// <param name="Rate">
/// Unit rate. Required (and &gt; 0) for MaterialReceipt; IGNORED for issues/transfers, where the
/// FIFO engine computes the valuation rate from the open cost layers.
/// </param>
public sealed record StockPostingLine(Guid ItemId, decimal Qty, decimal? Rate);

/// <summary>
/// Everything the posting engine needs to atomically post one stock voucher (decision D4).
/// There is no draft state: the request IS the posting.
/// </summary>
public sealed record StockPostingRequest(
    Guid CompanyId,
    StockEntryType EntryType,
    DateOnly PostingDate,
    Guid WarehouseId,
    Guid? TargetWarehouseId,
    IReadOnlyList<StockPostingLine> Lines);

/// <summary>
/// Application-level orchestration of a stock posting (decision D4): resolves companies,
/// warehouses, items and GL accounts through repositories, runs the pure FIFO domain logic,
/// writes StockLedgerEntry + balanced GLEntry rows and assigns a gapless voucher number - all
/// inside ONE database transaction. No EF Core here: repositories are Domain contracts only
/// (Constitution Article I.3).
/// </summary>
public interface IStockPostingService
{
    /// <exception cref="StockValidationException">A stock/domain invariant was violated (4xx).</exception>
    /// <exception cref="InsufficientStockException">Negative stock forbidden and layers run dry (Task 3.3).</exception>
    /// <exception cref="StockPostingConfigurationException">GL/company configuration is missing (500).</exception>
    /// <exception cref="System.NotSupportedException">Item uses a valuation method other than FIFO.</exception>
    Task<StockEntryPostingDto> PostAsync(StockPostingRequest request, CancellationToken cancellationToken = default);
}
