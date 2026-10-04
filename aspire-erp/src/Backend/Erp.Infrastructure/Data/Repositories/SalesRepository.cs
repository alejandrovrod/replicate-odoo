using System.Data;
using System.Data.Common;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Erp.Infrastructure.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ISalesOrderRepository"/> and
/// <see cref="IDeliveryNoteRepository"/>: the selling workflow, the delivery-note persistence and
/// the two per-table gapless generators (Constitution III.4).
/// </summary>
/// <remarks>
/// ONE class serves both contracts because both write against the SAME scoped AppDbContext - so
/// the posting transaction opened by either ExecuteInTransactionAsync is ambient for the other
/// (the delivery note, the stock ledger, the GL and the sales order commit as one unit). There is
/// deliberately NO manual <c>.Where(e =&gt; e.TenantId == ...)</c> on LINQ queries (Constitution
/// II.3 - the global query filter does it); the raw SQL numbering statement scopes by TenantId
/// itself because EF query filters do NOT apply to raw SQL.
/// </remarks>
public sealed class SalesRepository : ISalesOrderRepository, IDeliveryNoteRepository
{
    // Table/column names are compile-time constants interpolated into the raw SQL (they cannot be
    // parameterized); each public Next* method passes only its own literals. SalesOrder numbers
    // come from the OrderNumber column (plan.md §1), delivery notes from VoucherNo (§1.6).
    private const string OrderTable = "dbo.SalesOrder";
    private const string OrderColumn = "OrderNumber";
    private const string DeliveryTable = "dbo.DeliveryNote";
    private const string DeliveryColumn = "VoucherNo";

    private readonly AppDbContext _dbContext;

    public SalesRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // Both interfaces expose the same contract: one DbContext, therefore one ambient transaction.
    Task<T> ISalesOrderRepository.ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
        => ExecuteInTransactionAsync(operation, cancellationToken);

    Task<T> IDeliveryNoteRepository.ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
        => ExecuteInTransactionAsync(operation, cancellationToken);

    private async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
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

    Task<string> ISalesOrderRepository.NextOrderNumberAsync(
        Guid companyId, int year, CancellationToken cancellationToken)
        => NextDocumentNumberAsync(OrderTable, OrderColumn, "SO", companyId, year, cancellationToken);

    Task<string> IDeliveryNoteRepository.NextDeliveryVoucherNumberAsync(
        Guid companyId, int year, CancellationToken cancellationToken)
        => NextDocumentNumberAsync(DeliveryTable, DeliveryColumn, "DN", companyId, year, cancellationToken);

    /// <summary>
    /// SELECT MAX(column) WITH (UPDLOCK, HOLDLOCK) inside the AMBIENT transaction, scoped to
    /// (TenantId, CompanyId, PREFIX-YYYY). The range lock serializes concurrent sequences per
    /// company/year/document table, and because it lives inside the same transaction it is
    /// released by COMMIT or ROLLBACK - a rolled-back operation consumes NO number.
    /// </summary>
    private async Task<string> NextDocumentNumberAsync(
        string table,
        string column,
        string prefix,
        Guid companyId,
        int year,
        CancellationToken cancellationToken)
    {
        var transaction = _dbContext.Database.CurrentTransaction
            ?? throw new InvalidOperationException(
                "Document numbering must run inside the creation/posting transaction (Constitution III.4): "
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
            + "WHERE TenantId = @TenantId AND CompanyId = @CompanyId AND " + column + " LIKE @Pattern;";
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
                    $"Stored document number '{max}' does not follow the PREFIX-YYYY-NNNNN format.");
            }

            if (currentSequence >= 99999)
            {
                throw new InvalidOperationException(
                    $"Document sequence for '{prefix}-{year}' is exhausted (max 99999).");
            }

            nextSequence = currentSequence + 1;
        }

        return $"{prefix}-{year}-{nextSequence:D5}";
    }

    // ------------------------------------------------------------------------------- persistence

    public async Task AddOrderAsync(SalesOrder order, CancellationToken cancellationToken = default)
    {
        await _dbContext.SalesOrders.AddAsync(order, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateOrderAsync(SalesOrder order, CancellationToken cancellationToken = default)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Another request transitioned the order (or delivered against it) between our load and
            // this save - the RowVersion WHERE clause matched 0 rows. Translate EF's exception into
            // the typed domain failure the handlers convert into a 409 Result.Failure, mirroring
            // PurchaseRepository.UpdateOrderAsync.
            throw new ConcurrencyConflictException(nameof(SalesOrder), order.Id, ex);
        }
    }

    public async Task AddDeliveryNoteAsync(DeliveryNote deliveryNote, CancellationToken cancellationToken = default)
    {
        await _dbContext.DeliveryNotes.AddAsync(deliveryNote, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    // ------------------------------------------------------------------------------------- reads

    public async Task<SalesOrder?> GetOrderByIdAsync(
        Guid salesOrderId,
        CancellationToken cancellationToken = default)
        => await _dbContext.SalesOrders
            .Include(o => o.Lines)

            // Customer rides along: ICustomerRepository exposes no batch read (Task 5.1 is frozen),
            // so the aggregate carries the identity the SalesOrderDto mapper needs in ONE query.
            .Include(o => o.Customer)
            .FirstOrDefaultAsync(o => o.Id == salesOrderId, cancellationToken);

    public async Task<IReadOnlyList<SalesOrder>> GetRecentOrdersByCompanyAsync(
        Guid companyId,
        int limit,
        CancellationToken cancellationToken = default)
        => await _dbContext.SalesOrders
            .Where(o => o.CompanyId == companyId)
            .Include(o => o.Lines)
            .Include(o => o.Customer)
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<DeliveryNote?> GetDeliveryNoteByIdAsync(
        Guid deliveryNoteId,
        CancellationToken cancellationToken = default)
        => await _dbContext.DeliveryNotes
            .Include(d => d.Lines)
            .FirstOrDefaultAsync(d => d.Id == deliveryNoteId, cancellationToken);

    public async Task<IReadOnlyList<DeliveryNote>> GetRecentDeliveryNotesByCompanyAsync(
        Guid companyId,
        int limit,
        CancellationToken cancellationToken = default)
        => await _dbContext.DeliveryNotes
            .Where(d => d.CompanyId == companyId)
            .Include(d => d.Lines)
            .OrderByDescending(d => d.CreatedAt)
            .ThenByDescending(d => d.Id)
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
