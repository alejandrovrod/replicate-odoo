using Erp.Application.DTOs;

namespace Erp.Application.Services;

/// <summary>Everything the manufacture posting needs to complete one work order atomically.</summary>
/// <param name="CompanyId">Company that owns the work order and its warehouses.</param>
/// <param name="WorkOrderId">InProcess work order being completed.</param>
/// <param name="ProducedQuantity">Finished units received; within (0, QuantityToProduce].</param>
/// <param name="PostingDate">Accounting date of the voucher and of its GL lines.</param>
public sealed record WorkOrderCompletionRequest(
    Guid CompanyId,
    Guid WorkOrderId,
    decimal ProducedQuantity,
    DateOnly PostingDate);

/// <summary>
/// Application-level orchestration of a manufacture posting (Task 9.4, spec MF-03): WIP
/// consumption at FIFO cost, operating-cost capitalization from the BOM's planned operations,
/// finished-goods receipt at the cost-engine unit rate, and the balanced three-leg voucher
/// (Dr 1330 / Cr 1320 / Cr 5210) - all inside ONE database transaction. No EF Core here:
/// repositories are Domain contracts only (Constitution Article I.3).
/// </summary>
public interface IManufacturingPostingService
{
    /// <exception cref="Erp.Domain.Exceptions.ManufacturingValidationException">A manufacturing invariant was violated (4xx).</exception>
    /// <exception cref="Erp.Domain.Exceptions.StockValidationException">A stock/master invariant was violated (4xx).</exception>
    /// <exception cref="Erp.Domain.Exceptions.InsufficientStockException">WIP cannot cover the consumption (Task MF-06).</exception>
    /// <exception cref="Erp.Domain.Exceptions.FiscalPeriodLockedException">Posting date is inside the frozen period.</exception>
    /// <exception cref="Erp.Domain.Exceptions.StockPostingConfigurationException">GL/warehouse configuration is missing (500).</exception>
    /// <exception cref="System.NotSupportedException">An item uses a valuation method other than FIFO.</exception>
    Task<StockEntryPostingDto> CompleteAsync(WorkOrderCompletionRequest request, CancellationToken cancellationToken = default);
}
