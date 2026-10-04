using Erp.Domain.Entities;
using Erp.Domain.Exceptions;

namespace Erp.Domain.Repositories;

/// <summary>
/// Data-access contract for the buying documents (Tasks 4.1-4.3): purchase order workflow,
/// receipt/invoice persistence, the three per-table gapless voucher generators (Constitution
/// III.4) and the reads the posting engine needs for three-way matching. Implemented by
/// Erp.Infrastructure.Data.Repositories.PurchaseRepository.
/// </summary>
/// <remarks>
/// All writes happen through <see cref="ExecuteInTransactionAsync{T}"/> so the voucher number, the
/// document, its StockLedgerEntry rows and its GLEntry rows commit atomically. The three
/// Next*VoucherNumberAsync methods are per-TABLE (the raw MAX query is hardcoded per document
/// table, mirroring IStockRepository.NextVoucherNumberAsync) and must run inside the ambient
/// posting transaction - the UPDLOCK/HOLDLOCK range lock protects the sequence.
/// </remarks>
public interface IPurchaseRepository
{
    /// <summary>
    /// Runs <paramref name="operation"/> inside ONE database transaction and commits only when it
    /// returns; any exception rolls the whole posting back. Joins an already-open transaction so
    /// nested calls compose instead of dead-locking (same contract as IStockRepository).
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);

    /// <summary>Next gapless purchase-order number, e.g. prefix "PO" + 2026 -&gt; "PO-2026-00001" (ambient transaction required).</summary>
    Task<string> NextOrderVoucherNumberAsync(Guid companyId, string prefix, int year, CancellationToken cancellationToken = default);

    /// <summary>Next gapless purchase-receipt number, e.g. prefix "PR" + 2026 -&gt; "PR-2026-00001" (ambient transaction required).</summary>
    Task<string> NextReceiptVoucherNumberAsync(Guid companyId, string prefix, int year, CancellationToken cancellationToken = default);

    /// <summary>Next gapless purchase-invoice number, e.g. prefix "PINV" + 2026 -&gt; "PINV-2026-00001" (ambient transaction required).</summary>
    Task<string> NextInvoiceVoucherNumberAsync(Guid companyId, string prefix, int year, CancellationToken cancellationToken = default);

    /// <summary>Persists a Draft purchase order with its lines (inside the ambient creation transaction).</summary>
    Task AddOrderAsync(PurchaseOrder order, CancellationToken cancellationToken = default);

    /// <summary>Saves workflow status transitions of an already-tracked order (inside the ambient transaction).</summary>
    Task UpdateOrderAsync(PurchaseOrder order, CancellationToken cancellationToken = default);

    /// <summary>Persists a posted purchase receipt with its lines (inside the ambient posting transaction).</summary>
    Task AddReceiptAsync(PurchaseReceipt receipt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a posted purchase invoice with its lines (inside the ambient posting transaction).
    /// </summary>
    Task AddInvoiceAsync(PurchaseInvoice invoice, CancellationToken cancellationToken = default);

    /// <summary>The order with its lines, or null when it does not exist in this tenant.</summary>
    Task<PurchaseOrder?> GetOrderByIdAsync(Guid purchaseOrderId, CancellationToken cancellationToken = default);

    /// <summary>Most recent purchase orders of a company (newest first) with lines.</summary>
    Task<IReadOnlyList<PurchaseOrder>> GetRecentOrdersByCompanyAsync(Guid companyId, int limit, CancellationToken cancellationToken = default);

    /// <summary>The receipt with its lines and navigation to its order, or null.</summary>
    Task<PurchaseReceipt?> GetReceiptByIdAsync(Guid purchaseReceiptId, CancellationToken cancellationToken = default);

    /// <summary>Most recent purchase receipts of a company (newest first) with lines.</summary>
    Task<IReadOnlyList<PurchaseReceipt>> GetRecentReceiptsByCompanyAsync(Guid companyId, int limit, CancellationToken cancellationToken = default);

    /// <summary>Fetches receipt lines with their parent receipts.</summary>
    Task<IReadOnlyList<PurchaseReceiptLine>> GetReceiptLinesByIdsAsync(IEnumerable<Guid> receiptLineIds, CancellationToken cancellationToken = default);

    /// <summary>Sum of all previously billed quantities for a given receipt line.</summary>
    Task<decimal> GetBilledQuantityForReceiptLineAsync(Guid purchaseReceiptLineId, CancellationToken cancellationToken = default);

    /// <summary>Most recent purchase invoices of a company (newest first) with lines.</summary>
    Task<IReadOnlyList<PurchaseInvoice>> GetRecentInvoicesByCompanyAsync(Guid companyId, int limit, CancellationToken cancellationToken = default);

    /// <summary>The invoice with its lines, or null when it does not exist in this tenant (spec BY-05).</summary>
    Task<PurchaseInvoice?> GetInvoiceByIdAsync(Guid purchaseInvoiceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves status/amount transitions of an already-tracked invoice (spec BY-05). Translates the
    /// RowVersion mismatch into <see cref="ConcurrencyConflictException"/> like UpdateOrderAsync.
    /// </summary>
    Task UpdateInvoiceAsync(PurchaseInvoice invoice, CancellationToken cancellationToken = default);
}
