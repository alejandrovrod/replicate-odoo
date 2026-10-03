using System.Data;
using System.Data.Common;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
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
        => NextVoucherNumberAsync(OrderTable, companyId, prefix, year, cancellationToken);

    public Task<string> NextReceiptVoucherNumberAsync(
        Guid companyId, string prefix, int year, CancellationToken cancellationToken = default)
        => NextVoucherNumberAsync(ReceiptTable, companyId, prefix, year, cancellationToken);

    public Task<string> NextInvoiceVoucherNumberAsync(
        Guid companyId, string prefix, int year, CancellationToken cancellationToken = default)
        => NextVoucherNumberAsync(InvoiceTable, companyId, prefix, year, cancellationToken);

    /// <summary>
    /// SELECT MAX(VoucherNo) WITH (UPDLOCK, HOLDLOCK) inside the AMBIENT posting transaction,
    /// scoped to (TenantId, CompanyId, prefix-year). The range lock serializes concurrent
    /// sequences per company/year/document table, and because it lives inside the same
    /// transaction it is released by COMMIT or ROLLBACK - a rolled-back posting consumes NO number.
    /// </summary>
    private async Task<string> NextVoucherNumberAsync(
        string table,
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
            $"SELECT MAX(VoucherNo) FROM {table} WITH (UPDLOCK, HOLDLOCK) "
            + "WHERE TenantId = @TenantId AND CompanyId = @CompanyId AND VoucherNo LIKE @Pattern;";
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

    public async Task AddReceiptAsync(PurchaseReceipt receipt, CancellationToken cancellationToken = default)
    {
        await _dbContext.PurchaseReceipts.AddAsync(receipt, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddInvoiceAsync(PurchaseInvoice invoice, CancellationToken cancellationToken = default)
    {
        try
        {
            await _dbContext.PurchaseInvoices.AddAsync(invoice, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // The unique (TenantId, PurchaseReceiptId) index is the hard backstop of the
            // ONE-invoice-per-receipt rule (Task 4.3): translate the race into a domain failure
            // instead of leaking an EF/SQL exception to the API layer.
            throw new PurchaseValidationException(
                PurchaseErrorCodes.InvoiceAlreadyExists,
                $"Receipt '{invoice.PurchaseReceiptId}' already has a purchase invoice "
                + "(one invoice per receipt, full three-way match).");
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };

    // ------------------------------------------------------------------------------------- reads

    public async Task<PurchaseOrder?> GetOrderByIdAsync(
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default)
        => await _dbContext.PurchaseOrders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == purchaseOrderId, cancellationToken);

    public async Task<IReadOnlyList<PurchaseOrder>> GetRecentOrdersByCompanyAsync(
        Guid companyId,
        int limit,
        CancellationToken cancellationToken = default)
        => await _dbContext.PurchaseOrders
            .Where(o => o.CompanyId == companyId)
            .Include(o => o.Lines)
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<PurchaseReceipt?> GetReceiptByIdAsync(
        Guid purchaseReceiptId,
        CancellationToken cancellationToken = default)
        => await _dbContext.PurchaseReceipts
            .Include(r => r.Lines)
            .Include(r => r.PurchaseOrder)
            .FirstOrDefaultAsync(r => r.Id == purchaseReceiptId, cancellationToken);

    public async Task<IReadOnlyList<PurchaseReceipt>> GetRecentReceiptsByCompanyAsync(
        Guid companyId,
        int limit,
        CancellationToken cancellationToken = default)
        => await _dbContext.PurchaseReceipts
            .Where(r => r.CompanyId == companyId)
            .Include(r => r.Lines)
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<bool> ReceiptHasInvoiceAsync(
        Guid purchaseReceiptId,
        CancellationToken cancellationToken = default)
        => await _dbContext.PurchaseInvoices.AnyAsync(
            i => i.PurchaseReceiptId == purchaseReceiptId,
            cancellationToken);

    public async Task<IReadOnlyList<PurchaseInvoice>> GetRecentInvoicesByCompanyAsync(
        Guid companyId,
        int limit,
        CancellationToken cancellationToken = default)
        => await _dbContext.PurchaseInvoices
            .Where(i => i.CompanyId == companyId)
            .Include(i => i.Lines)
            .OrderByDescending(i => i.CreatedAt)
            .ThenByDescending(i => i.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
