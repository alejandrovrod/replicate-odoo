using System.Data;
using System.Data.Common;
using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using Erp.Infrastructure.Data.Pagination;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Erp.Infrastructure.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IPurchaseRepository"/>: the buying workflow, receipt and
/// invoice persistence and the three per-table gapless voucher generators (Constitution III.4).
/// </summary>
/// <remarks>
/// Every write runs inside <see cref="ExecuteInTransactionAsync{T}"/> so a posting is atomic.
/// There is deliberately NO manual <c>.Where(e =&gt; e.TenantId == ...)</c> on LINQ queries
/// (Constitution II.3 - the global query filter does it); the raw SQL voucher statements scope
/// by TenantId themselves because EF query filters do NOT apply to raw SQL.
/// </remarks>
public sealed class PurchaseRepository : IPurchaseRepository
{
    // Table names are compile-time constants interpolated into the raw SQL (a table name cannot
    // be parameterized); each public Next* method passes only its own literal.
    private const string OrderTable = "dbo.PurchaseOrder";
    private const string ReceiptTable = "dbo.PurchaseReceipt";
    private const string InvoiceTable = "dbo.PurchaseInvoice";

    // Number column per table: PurchaseOrder stores its gapless sequence in OrderNumber (the
    // column predates the voucher vocabulary), while receipts and invoices use VoucherNo.
    // Parameterized the same way SalesRepository does for SalesOrder.OrderNumber - MAX(VoucherNo)
    // against dbo.PurchaseOrder fails with SQL error 207 (invalid column name).
    private const string OrderColumn = "OrderNumber";
    private const string VoucherColumn = "VoucherNo";

    private readonly AppDbContext _dbContext;

    public PurchaseRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        // Join an already-open transaction instead of creating a nested one (same DbContext instance).
        if (_dbContext.Database.CurrentTransaction is not null)
        {
            return await operation(cancellationToken);
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var result = await operation(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    // ------------------------------------------------------------------- voucher numbering (III.4)

    public Task<string> NextOrderVoucherNumberAsync(
        Guid companyId, string prefix, int year, CancellationToken cancellationToken = default)
        => NextVoucherNumberAsync(OrderTable, OrderColumn, companyId, prefix, year, cancellationToken);

    public Task<string> NextReceiptVoucherNumberAsync(
        Guid companyId, string prefix, int year, CancellationToken cancellationToken = default)
        => NextVoucherNumberAsync(ReceiptTable, VoucherColumn, companyId, prefix, year, cancellationToken);

    public Task<string> NextInvoiceVoucherNumberAsync(
        Guid companyId, string prefix, int year, CancellationToken cancellationToken = default)
        => NextVoucherNumberAsync(InvoiceTable, VoucherColumn, companyId, prefix, year, cancellationToken);

    /// <summary>
    /// SELECT MAX(<paramref name="column"/>) WITH (UPDLOCK, HOLDLOCK) inside the AMBIENT posting
    /// transaction, scoped to (TenantId, CompanyId, prefix-year). The range lock serializes concurrent
    /// sequences per company/year/document table, and because it lives inside the same
    /// transaction it is released by COMMIT or ROLLBACK - a rolled-back posting consumes NO number.
    /// </summary>
    private async Task<string> NextVoucherNumberAsync(
        string table,
        string column,
        Guid companyId,
        string prefix,
        int year,
        CancellationToken cancellationToken)
    {
        var transaction = _dbContext.Database.CurrentTransaction
            ?? throw new InvalidOperationException(
                "Voucher numbering must run inside the posting transaction (Constitution III.4): "
                + "outside one the UPDLOCK/HOLDLOCK range lock cannot protect the sequence.");

        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await _dbContext.Database.OpenConnectionAsync(cancellationToken);
        }

        // EF's global query filter does NOT apply to raw SQL, so the tenant scope is written out
        // explicitly here (it reproduces exactly what the filter would have done).
        var tenantId = _dbContext.CurrentTenantId;
        var pattern = $"{prefix}-{year}-%";

        await using var command = connection.CreateCommand();
        command.CommandText =
            $"SELECT MAX({column}) FROM {table} WITH (UPDLOCK, HOLDLOCK) "
            + $"WHERE TenantId = @TenantId AND CompanyId = @CompanyId AND {column} LIKE @Pattern;";
        command.Transaction = transaction.GetDbTransaction();

        AddParameter(command, "@TenantId", tenantId);
        AddParameter(command, "@CompanyId", companyId);
        AddParameter(command, "@Pattern", pattern);

        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        var max = scalar as string;

        var nextSequence = 1;
        if (!string.IsNullOrEmpty(max))
        {
            // Fixed 5-digit zero padding keeps lexicographic and numeric order identical.
            var separator = max.LastIndexOf('-');
            if (separator < 0 || !int.TryParse(max[(separator + 1)..], out var currentSequence))
            {
                throw new InvalidOperationException(
                    $"Stored voucher number '{max}' does not follow the PREFIX-YYYY-NNNNN format.");
            }

            if (currentSequence >= 99999)
            {
                throw new InvalidOperationException(
                    $"Voucher sequence for '{prefix}-{year}' is exhausted (max 99999).");
            }

            nextSequence = currentSequence + 1;
        }

        return $"{prefix}-{year}-{nextSequence:D5}";
    }

    // ------------------------------------------------------------------------------- persistence

    public async Task AddOrderAsync(PurchaseOrder order, CancellationToken cancellationToken = default)
    {
        await _dbContext.PurchaseOrders.AddAsync(order, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateOrderAsync(PurchaseOrder order, CancellationToken cancellationToken = default)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Spec BY-06: another request transitioned the order (or edited it) between our load
            // and this save - the RowVersion WHERE clause matched 0 rows. Translate EF's exception
            // into the typed domain failure the handlers convert into a 409 Result.Failure,
            // mirroring the unique-violation translation in AddInvoiceAsync above.
            throw new ConcurrencyConflictException(nameof(PurchaseOrder), order.Id, ex);
        }
    }

    public async Task ReplaceOrderItemsAsync(
        PurchaseOrder order,
        IReadOnlyList<PurchaseOrderItem> newItems,
        CancellationToken cancellationToken = default)
    {
        // Explicit states (see IPurchaseRepository.ReplaceOrderItemsAsync): a bare
        // order.Items.Clear() + re-add would leave the new lines tracked as Modified - EF then
        // issues UPDATE ... WHERE Id = <fresh Guid> against rows that do not exist yet, reads 0
        // affected rows and raises DbUpdateConcurrencyException (a spurious 409). Marking the old
        // lines Deleted and the new ones Added is the same explicit path AddOrderAsync takes at
        // creation. The header edits stay tracked and commit in the same SaveChanges.
        foreach (var line in order.Items.ToList())
        {
            _dbContext.Entry(line).State = EntityState.Deleted;
        }

        order.Items.Clear();

        foreach (var line in newItems)
        {
            order.Items.Add(line);
            _dbContext.Entry(line).State = EntityState.Added;
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(nameof(PurchaseOrder), order.Id, ex);
        }
    }

    public async Task AddReceiptAsync(PurchaseReceipt receipt, CancellationToken cancellationToken = default)
    {
        await _dbContext.PurchaseReceipts.AddAsync(receipt, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddInvoiceAsync(PurchaseInvoice invoice, CancellationToken cancellationToken = default)
    {
        await _dbContext.PurchaseInvoices.AddAsync(invoice, cancellationToken);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // IX_PurchaseInvoice_Company_BillNumber (verify W9) is the hard backstop of the
            // duplicate-bill rule: the posting pre-check can be raced by a concurrent insert, so
            // translate the race into the same domain failure instead of leaking an EF/SQL
            // exception (mirrors CustomerRepository.AddAsync).
            _dbContext.Entry(invoice).State = EntityState.Detached;
            throw new PurchaseValidationException(
                PurchaseErrorCodes.InvoiceAlreadyExists,
                $"An invoice with bill number '{invoice.BillNumber}' already exists for this company.");
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };

    public async Task<bool> InvoiceBillNumberExistsAsync(
        Guid companyId, string billNumber, CancellationToken cancellationToken = default)
        => await _dbContext.PurchaseInvoices.AnyAsync(
            i => i.CompanyId == companyId && i.BillNumber == billNumber,
            cancellationToken);

    public async Task UpdateInvoiceAsync(PurchaseInvoice invoice, CancellationToken cancellationToken = default)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Spec BY-05: another request cancelled/edited the invoice between our load and this
            // save, so the RowVersion WHERE clause matched 0 rows. Surface it as the typed 409
            // failure (mirrors UpdateOrderAsync).
            throw new ConcurrencyConflictException(nameof(PurchaseInvoice), invoice.Id, ex);
        }
    }

    // ------------------------------------------------------------------------------------- reads

    public async Task<PurchaseOrder?> GetOrderByIdAsync(
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default)
        => await _dbContext.PurchaseOrders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == purchaseOrderId, cancellationToken);

    public async Task<PagedResult<PurchaseOrder>> GetRecentOrdersByCompanyAsync(
        Guid companyId,
        PagedRequest paging,
        CancellationToken cancellationToken = default)
        => await _dbContext.PurchaseOrders
            .Where(o => o.CompanyId == companyId)
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.Id)
            .ToPagedResultAsync(paging, cancellationToken);

    public async Task<PurchaseReceipt?> GetReceiptByIdAsync(
        Guid purchaseReceiptId,
        CancellationToken cancellationToken = default)
        => await _dbContext.PurchaseReceipts
            .Include(r => r.Lines)
            .Include(r => r.PurchaseOrder)
            .FirstOrDefaultAsync(r => r.Id == purchaseReceiptId, cancellationToken);

    public async Task<PagedResult<PurchaseReceipt>> GetRecentReceiptsByCompanyAsync(
        Guid companyId,
        PagedRequest paging,
        CancellationToken cancellationToken = default)
        => await _dbContext.PurchaseReceipts
            .Where(r => r.CompanyId == companyId)
            .Include(r => r.Lines)
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.Id)
            .ToPagedResultAsync(paging, cancellationToken);

    public async Task<IReadOnlyList<PurchaseReceiptLine>> GetReceiptLinesByIdsAsync(
        IEnumerable<Guid> receiptLineIds,
        CancellationToken cancellationToken = default)
    {
        var ids = receiptLineIds.Distinct().ToList();

        // Spec BY-06 row locking: take the range lock BEFORE the EF load, so the lock is held
        // ahead of the billed-quantity SUMs that happen later in the posting transaction.
        await LockReceiptLinesForBillingAsync(ids, cancellationToken);

        return await _dbContext.Set<PurchaseReceiptLine>()
            .Include(l => l.PurchaseReceipt)
            .ThenInclude(r => r!.PurchaseOrder)
            .Where(l => ids.Contains(l.Id))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// SELECT ... WITH (UPDLOCK, HOLDLOCK) over the requested <c>dbo.PurchaseReceiptLine</c> rows
    /// inside the AMBIENT posting transaction - the serializing half of spec BY-06. Two clerks
    /// billing the SAME receipt line block here instead of racing: the loser waits until the winner
    /// COMMITs, then re-reads the billed quantity with the lock already held and fails the
    /// cumulative three-way match with <see cref="OverbillingNotAllowedException"/> (it can no
    /// longer read a stale "nothing billed yet" snapshot - the voucher-number UPDLOCK that runs
    /// afterwards would be too late to protect that read).
    /// </summary>
    /// <remarks>
    /// The id list mirrors the invoice's own receipt lines (a handful per bill, each one its own
    /// <c>@pN</c> parameter - parameters are never interpolated), so the IN list stays far below
    /// SQL Server's 2100-parameter ceiling. Without an open transaction there is nothing to hold
    /// the lock against, and the statement is skipped: the plain EF load below still applies the
    /// read semantics the caller expects. <c>dbo.PurchaseReceiptLine</c> carries no TenantId column,
    /// so - unlike the voucher statements - there is no tenant scope to write out explicitly.
    /// </remarks>
    private async Task LockReceiptLinesForBillingAsync(
        IReadOnlyList<Guid> receiptLineIds,
        CancellationToken cancellationToken)
    {
        var transaction = _dbContext.Database.CurrentTransaction;
        if (transaction is null || receiptLineIds.Count == 0)
        {
            return;
        }

        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await _dbContext.Database.OpenConnectionAsync(cancellationToken);
        }

        var parameters = string.Join(", ", receiptLineIds.Select((_, index) => $"@p{index}"));

        await using var command = connection.CreateCommand();
        command.CommandText =
            $"SELECT Id FROM dbo.PurchaseReceiptLine WITH (UPDLOCK, HOLDLOCK) WHERE Id IN ({parameters});";
        command.Transaction = transaction.GetDbTransaction();

        for (var index = 0; index < receiptLineIds.Count; index++)
        {
            AddParameter(command, $"@p{index}", receiptLineIds[index]);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<decimal> GetBilledQuantityForReceiptLineAsync(
        Guid purchaseReceiptLineId,
        CancellationToken cancellationToken = default)
        => await _dbContext.Set<PurchaseInvoiceLine>()
            .Where(l => l.PurchaseReceiptLineId == purchaseReceiptLineId)
            .SumAsync(l => (decimal?)l.Qty, cancellationToken) ?? 0m;

    public async Task<PagedResult<PurchaseInvoice>> GetRecentInvoicesByCompanyAsync(
        Guid companyId,
        PagedRequest paging,
        CancellationToken cancellationToken = default)
        => await _dbContext.PurchaseInvoices
            .Where(i => i.CompanyId == companyId)
            .Include(i => i.Lines)
            .OrderByDescending(i => i.CreatedAt)
            .ThenByDescending(i => i.Id)
            .ToPagedResultAsync(paging, cancellationToken);

    public async Task<PurchaseInvoice?> GetInvoiceByIdAsync(
        Guid purchaseInvoiceId,
        CancellationToken cancellationToken = default)
        => await _dbContext.PurchaseInvoices
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == purchaseInvoiceId, cancellationToken);

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
