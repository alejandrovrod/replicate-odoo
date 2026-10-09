using Erp.Domain.Entities;

namespace Erp.Domain.Repositories;

/// <summary>
/// Read-only Kardex source for the Stock Ledger report (tasks.md 7.1, ERPNext
/// <c>stock_ledger.py</c> parity): chronological movement rows in a period, optionally
/// narrowed to one item and/or one warehouse, newest-first capped for grid rendering.
/// </summary>
/// <remarks>
/// Segregated from <see cref="IStockRepository"/> on purpose: reporting handlers must only
/// depend on read contracts (no Save/Update surface), so a report can never mutate what it
/// measures. Implemented by <c>StockRepository</c> through the same scoped
/// <c>AppDbContext</c> (the SalesRepository dual-contract precedent).
/// </remarks>
public interface IStockLedgerReportRepository
{
    /// <summary>
    /// Kardex rows of a company with <c>From &lt;= PostingDate &lt;= To</c>, ordered
    /// chronologically (PostingDate, CreatedAt, Id), with Item/Warehouse navigations loaded.
    /// </summary>
    Task<IReadOnlyList<StockLedgerEntry>> GetLedgerEntriesByCompanyAsync(
        Guid companyId,
        DateOnly from,
        DateOnly to,
        Guid? itemId,
        Guid? warehouseId,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Opening balance of one (item, warehouse) pair before <paramref name="from"/>:
    /// SUM(QtyChange) and SUM(Amount). One scalar query, never materialized rows.
    /// </summary>
    Task<(decimal Qty, decimal Value)> GetOpeningBalanceAsync(
        Guid itemId,
        Guid warehouseId,
        DateOnly from,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Read-only receivables source for the Aging report (tasks.md 7.1, ERPNext
/// <c>accounts_receivable.py</c> parity): open sales invoices (Outstanding &gt; 0,
/// Unpaid/PartiallyPaid) posted on or before the report date, with the Customer navigation
/// loaded, oldest due first.
/// </summary>
public interface IReceivableAgingRepository
{
    Task<IReadOnlyList<SalesInvoice>> GetOpenReceivablesByCompanyAsync(
        Guid companyId,
        DateOnly reportDate,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Read-only payables source for the Aging report (tasks.md 7.1): open purchase bills
/// (Outstanding &gt; 0, Unpaid/PartiallyPaid) posted on or before the report date, with the
/// Supplier navigation loaded, oldest due first.
/// </summary>
public interface IPayableAgingRepository
{
    Task<IReadOnlyList<PurchaseInvoice>> GetOpenPayablesByCompanyAsync(
        Guid companyId,
        DateOnly reportDate,
        CancellationToken cancellationToken = default);
}
