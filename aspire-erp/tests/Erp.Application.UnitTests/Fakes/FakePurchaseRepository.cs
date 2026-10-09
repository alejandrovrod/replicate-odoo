using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.UnitTests.Fakes;

/// <summary>
/// In-memory <see cref="IPurchaseRepository"/>: gapless vouchers come from a per-(prefix, year)
/// sequence, the posting transaction simply executes its callback, invoices accumulate against the
/// same receipt line exactly like production (the cumulative three-way match is the only guard) and
/// every persisted aggregate is captured so tests can assert on PurchaseOrder / PurchaseReceipt /
/// PurchaseInvoice rows without a database.
/// </summary>
public sealed class FakePurchaseRepository : IPurchaseRepository, IPayableAgingRepository
{
    private readonly List<PurchaseOrder> _orders = new();
    private readonly List<PurchaseReceipt> _receipts = new();
    private readonly List<PurchaseInvoice> _invoices = new();
    private readonly Dictionary<(string Prefix, int Year), int> _sequences = new();

    public IReadOnlyList<PurchaseOrder> Orders => _orders;

    public IReadOnlyList<PurchaseReceipt> Receipts => _receipts;

    public IReadOnlyList<PurchaseInvoice> Invoices => _invoices;

    /// <summary>Number of transactions opened (proves the posting runs inside ONE transaction).</summary>
    public int TransactionCount { get; private set; }

    /// <summary>Pre-loads an order (workflow state tests need Draft/Received/Billed orders directly).</summary>
    public void SeedOrder(params PurchaseOrder[] orders) => _orders.AddRange(orders);

    /// <summary>Pre-loads a receipt (invoice tests can start from an already-posted receipt).</summary>
    public void SeedReceipt(params PurchaseReceipt[] receipts) => _receipts.AddRange(receipts);

    /// <summary>Pre-loads an invoice (cancellation tests start from an already-posted bill).</summary>
    public void SeedInvoice(params PurchaseInvoice[] invoices) => _invoices.AddRange(invoices);

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        TransactionCount++;

        // The real repository rolls the whole transaction back when the operation throws; the
        // in-memory fake models that for the header mutations the cancel path performs BEFORE its
        // gates (status + outstanding), so rejection tests can assert the bill is untouched.
        var snapshot = _invoices.Select(i => (Invoice: i, i.Status, i.OutstandingAmount)).ToList();

        try
        {
            return await operation(cancellationToken);
        }
        catch
        {
            foreach (var (invoice, status, outstanding) in snapshot)
            {
                invoice.Status = status;
                invoice.OutstandingAmount = outstanding;
            }

            throw;
        }
    }

    public Task<string> NextOrderVoucherNumberAsync(
        Guid companyId, string prefix, int year, CancellationToken cancellationToken = default)
        => Task.FromResult(NextVoucherNumber(prefix, year));

    public Task<string> NextReceiptVoucherNumberAsync(
        Guid companyId, string prefix, int year, CancellationToken cancellationToken = default)
        => Task.FromResult(NextVoucherNumber(prefix, year));

    public Task<string> NextInvoiceVoucherNumberAsync(
        Guid companyId, string prefix, int year, CancellationToken cancellationToken = default)
        => Task.FromResult(NextVoucherNumber(prefix, year));

    private string NextVoucherNumber(string prefix, int year)
    {
        var key = (prefix, year);
        _sequences.TryGetValue(key, out var current);
        _sequences[key] = current + 1;
        return $"{prefix}-{year}-{current + 1:D5}";
    }

    public Task AddOrderAsync(PurchaseOrder order, CancellationToken cancellationToken = default)
    {
        _orders.Add(order);
        return Task.CompletedTask;
    }

    /// <summary>
    /// When set, the NEXT <c>UpdateOrderAsync</c> fails like the real repository does after a
    /// RowVersion mismatch (<c>DbUpdateConcurrencyException</c> translated to
    /// <see cref="ConcurrencyConflictException"/>); the flag resets itself so only one call fails.
    /// </summary>
    public bool FailNextOrderUpdate { get; set; }

    public Task UpdateOrderAsync(PurchaseOrder order, CancellationToken cancellationToken = default)
    {
        if (FailNextOrderUpdate)
        {
            FailNextOrderUpdate = false;
            throw new ConcurrencyConflictException(nameof(PurchaseOrder), order.Id);
        }

        // In-memory: the entity instance IS the store; workflow mutations are already applied.
        return Task.CompletedTask;
    }

    /// <summary>
    /// In-memory line rewrite: the entity instance IS the store, so swapping the collection is the
    /// equivalent of production's explicit Deleted/Added states (old lines out, new lines in) -
    /// totals stay the handler's responsibility.
    /// </summary>
    public Task ReplaceOrderItemsAsync(
        PurchaseOrder order,
        IReadOnlyList<PurchaseOrderItem> newItems,
        CancellationToken cancellationToken = default)
    {
        order.Items.Clear();
        foreach (var item in newItems)
        {
            order.Items.Add(item);
        }

        return Task.CompletedTask;
    }

    public Task AddReceiptAsync(PurchaseReceipt receipt, CancellationToken cancellationToken = default)
    {
        receipt.PurchaseOrder = _orders.FirstOrDefault(o => o.Id == receipt.PurchaseOrderId);
        _receipts.Add(receipt);
        return Task.CompletedTask;
    }

    public Task AddInvoiceAsync(PurchaseInvoice invoice, CancellationToken cancellationToken = default)
    {
        // Progressive billing is legal (several invoices may bill one receipt); the cumulative
        // three-way match in ThreeWayMatchValidator is the guard, exactly as in production.
        _invoices.Add(invoice);
        return Task.CompletedTask;
    }

    public Task<PurchaseOrder?> GetOrderByIdAsync(
        Guid purchaseOrderId, CancellationToken cancellationToken = default)
        => Task.FromResult(_orders.FirstOrDefault(o => o.Id == purchaseOrderId));

    // In-memory fakes ignore paging and return the whole seeded set (see FakeCustomerRepository).
    public Task<PagedResult<PurchaseOrder>> GetRecentOrdersByCompanyAsync(
        Guid companyId, PagedRequest paging, CancellationToken cancellationToken = default)
    {
        var items = _orders.Where(o => o.CompanyId == companyId).ToList();
        return Task.FromResult(new PagedResult<PurchaseOrder>(items, items.Count, paging.SafePageNumber, paging.SafePageSize));
    }

    public Task<PurchaseReceipt?> GetReceiptByIdAsync(
        Guid purchaseReceiptId, CancellationToken cancellationToken = default)
        => Task.FromResult(AttachOrder(_receipts.FirstOrDefault(r => r.Id == purchaseReceiptId)));

    public Task<PagedResult<PurchaseReceipt>> GetRecentReceiptsByCompanyAsync(
        Guid companyId, PagedRequest paging, CancellationToken cancellationToken = default)
    {
        var items = _receipts
            .Where(r => r.CompanyId == companyId)
            .Select(AttachOrder)
            .ToList()!;
        return Task.FromResult(new PagedResult<PurchaseReceipt>(items, items.Count, paging.SafePageNumber, paging.SafePageSize));
    }

    public Task<bool> ReceiptHasInvoiceAsync(
        Guid purchaseReceiptId, CancellationToken cancellationToken = default)
    {
        var receipt = _receipts.FirstOrDefault(r => r.Id == purchaseReceiptId);
        if (receipt == null) return Task.FromResult(false);
        var receiptLineIds = receipt.Lines.Select(l => l.Id).ToHashSet();
        return Task.FromResult(_invoices.SelectMany(i => i.Lines).Any(l => receiptLineIds.Contains(l.PurchaseReceiptLineId)));
    }

    public Task<PagedResult<PurchaseInvoice>> GetRecentInvoicesByCompanyAsync(
        Guid companyId, PagedRequest paging, CancellationToken cancellationToken = default)
    {
        var items = _invoices.Where(i => i.CompanyId == companyId).ToList();
        return Task.FromResult(new PagedResult<PurchaseInvoice>(items, items.Count, paging.SafePageNumber, paging.SafePageSize));
    }

    public Task<PurchaseInvoice?> GetInvoiceByIdAsync(
        Guid purchaseInvoiceId, CancellationToken cancellationToken = default)
        => Task.FromResult(_invoices.FirstOrDefault(i => i.Id == purchaseInvoiceId));

    public Task<IReadOnlyList<PurchaseInvoice>> GetOutstandingBySupplierAsync(
        Guid companyId, Guid supplierId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PurchaseInvoice>>(
            _invoices
                .Where(i => i.CompanyId == companyId
                    && i.SupplierId == supplierId
                    && i.OutstandingAmount > 0m
                    && (i.Status == PurchaseInvoiceStatus.Unpaid || i.Status == PurchaseInvoiceStatus.PartiallyPaid))
                .OrderBy(i => i.DueDate)
                .ThenBy(i => i.Id)
                .ToList());

    public Task<IReadOnlyList<PurchaseInvoice>> GetOpenPayablesByCompanyAsync(
        Guid companyId, DateOnly reportDate, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PurchaseInvoice>>(
            _invoices
                .Where(i => i.CompanyId == companyId
                    && i.PostingDate <= reportDate
                    && i.OutstandingAmount > 0m
                    && (i.Status == PurchaseInvoiceStatus.Unpaid || i.Status == PurchaseInvoiceStatus.PartiallyPaid))
                .OrderBy(i => i.DueDate)
                .ThenBy(i => i.Id)
                .ToList());

    /// <summary>
    /// When set, the NEXT <c>UpdateInvoiceAsync</c> fails like the real repository does after a
    /// RowVersion mismatch; the flag resets itself so only one call fails.
    /// </summary>
    public bool FailNextInvoiceUpdate { get; set; }

    public Task UpdateInvoiceAsync(PurchaseInvoice invoice, CancellationToken cancellationToken = default)
    {
        if (FailNextInvoiceUpdate)
        {
            FailNextInvoiceUpdate = false;
            throw new ConcurrencyConflictException(nameof(PurchaseInvoice), invoice.Id);
        }

        // In-memory: the entity instance IS the store; the cancel mutation is already applied.
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PurchaseReceiptLine>> GetReceiptLinesByIdsAsync(
        IEnumerable<Guid> lineIds, CancellationToken cancellationToken = default)
    {
        var ids = lineIds.ToHashSet();
        var lines = _receipts
            .SelectMany(r => r.Lines.Select(l => { l.PurchaseReceipt = r; return l; }))
            .Where(l => ids.Contains(l.Id))
            .ToList();
        return Task.FromResult<IReadOnlyList<PurchaseReceiptLine>>(lines);
    }

    public Task<decimal> GetBilledQuantityForReceiptLineAsync(
        Guid receiptLineId, CancellationToken cancellationToken = default)
    {
        var billed = _invoices
            .SelectMany(i => i.Lines)
            .Where(l => l.PurchaseReceiptLineId == receiptLineId)
            .Sum(l => l.Qty);
        return Task.FromResult(billed);
    }

    public Task<bool> InvoiceBillNumberExistsAsync(
        Guid companyId, string billNumber, CancellationToken cancellationToken = default)
        => Task.FromResult(_invoices.Any(
            i => i.CompanyId == companyId && i.BillNumber == billNumber));

    /// <summary>Reproduces the .Include(r => r.PurchaseOrder) of the real repository.</summary>
    private PurchaseReceipt? AttachOrder(PurchaseReceipt? receipt)
    {
        if (receipt is not null && receipt.PurchaseOrder is null && receipt.PurchaseOrderId is { } orderId)
        {
            receipt.PurchaseOrder = _orders.FirstOrDefault(o => o.Id == orderId);
        }

        return receipt;
    }
}

