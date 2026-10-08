using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;

namespace Erp.Application.Services;

/// <summary>One line of a purchase receipt posting request (qty in the item's Base UOM, rate values the stock).</summary>
public sealed record PurchaseReceiptPostingLine(Guid ItemId, decimal Qty, decimal Rate);

/// <summary>
/// Everything the buying posting engine needs to atomically post one purchase receipt
/// (Task 4.2). There is no draft state: the request IS the posting.
/// </summary>
public sealed record PurchaseReceiptPostingRequest(
    Guid CompanyId,
    Guid WarehouseId,
    Guid SupplierId,
    Guid? PurchaseOrderId,
    DateOnly PostingDate,
    IReadOnlyList<PurchaseReceiptPostingLine> Lines);

/// <summary>
/// One line of a purchase invoice posting request: bills ONE receipt line IN FULL
/// (<c>Qty</c> must equal the receipt line's quantity; <c>Rate</c> is the vendor's price).
/// </summary>
public sealed record PurchaseInvoicePostingLine(
    Guid PurchaseReceiptLineId,
    Guid ItemId,
    decimal Qty,
    decimal Rate);

/// <summary>
/// Everything the buying posting engine needs to atomically post one purchase invoice
/// (Task 4.3 / spec BY-01). There is no draft state: the request IS the posting.
/// </summary>
public sealed record PurchaseInvoicePostingRequest(
    Guid CompanyId,
    Guid SupplierId,
    string BillNumber,
    Guid? CurrencyId,
    DateOnly PostingDate,
    DateOnly DueDate,
    decimal TaxAmount,
    IReadOnlyList<PurchaseInvoicePostingLine> Lines);

/// <summary>
/// Application-level orchestration of the buying postings (Tasks 4.2/4.3): resolves companies,
/// warehouses, items, purchase documents and GL accounts through repositories, writes
/// StockLedgerEntry + balanced GLEntry rows and assigns gapless voucher numbers - all inside ONE
/// database transaction. No EF Core here: repositories are Domain contracts only (Constitution
/// Article I.3).
/// </summary>
public interface IPurchasePostingService
{
    /// <summary>Posts a receipt: +Kardex, Dr warehouse stock account / Cr Stock Received But Not Billed, order -&gt; Received.</summary>
    /// <exception cref="PurchaseValidationException">A buying/domain invariant was violated (4xx).</exception>
    /// <exception cref="StockValidationException">A stock concept was violated (company/warehouse/item, 4xx).</exception>
    /// <exception cref="PurchasePostingConfigurationException">GL/company configuration is missing (500).</exception>
    Task<PurchaseReceiptPostingDto> PostReceiptAsync(PurchaseReceiptPostingRequest request, CancellationToken cancellationToken = default);

    /// <summary>Posts an invoice: Dr Stock Received But Not Billed (receipt value) + Dr Input Tax + Dr/Cr price difference / Cr Accounts Payable, order -&gt; Billed.</summary>
    /// <exception cref="PurchaseValidationException">A buying/domain invariant was violated (4xx).</exception>
    /// <exception cref="StockValidationException">A stock concept was violated (company/item, 4xx).</exception>
    /// <exception cref="PurchasePostingConfigurationException">GL/company configuration is missing (500).</exception>
    Task<PurchaseInvoicePostingDto> PostInvoiceAsync(PurchaseInvoicePostingRequest request, CancellationToken cancellationToken = default);
}
