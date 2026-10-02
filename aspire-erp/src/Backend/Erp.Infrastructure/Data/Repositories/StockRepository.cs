using System.Data;
using System.Data.Common;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Erp.Infrastructure.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IStockRepository"/>: FIFO layer reads, stock balances,
/// voucher persistence and the gapless voucher number generator (Constitution III.4).
/// </summary>
/// <remarks>
/// Every write runs inside <see cref="ExecuteInTransactionAsync{T}"/> so a posting is atomic.
/// There is deliberately NO manual <c>.Where(e =&gt; e.TenantId == ...)</c> on LINQ queries
/// (Constitution II.3 - the global query filter does it); the ONE raw SQL statement below scopes
/// by TenantId itself because EF query filters do NOT apply to raw SQL, so leaving it out there
/// would be a cross-tenant leak.
/// </remarks>
public sealed class StockRepository : IStockRepository
{
    private readonly AppDbContext _dbContext;

    public StockRepository(AppDbContext dbContext)
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

    public async Task<IReadOnlyList<StockLedgerEntry>> GetFifoLayersAsync(
        Guid itemId,
        Guid warehouseId,
        DateOnly asOf,
        CancellationToken cancellationToken = default)
        => await _dbContext.StockLedgerEntries
            .Where(e => e.ItemId == itemId
                && e.WarehouseId == warehouseId
                && e.PostingDate <= asOf)
            .OrderBy(e => e.PostingDate)
            .ThenBy(e => e.CreatedAt)
            .ThenBy(e => e.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<StockBalance>> GetStockBalancesByCompanyAsync(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var warehouseIds = _dbContext.Warehouses
            .Where(w => w.CompanyId == companyId)
            .Select(w => w.Id);

        var rows = await _dbContext.StockLedgerEntries
            .Where(e => warehouseIds.Contains(e.WarehouseId))
            .GroupBy(e => new { e.ItemId, e.WarehouseId })
            .Select(g => new
            {
                g.Key.ItemId,
                g.Key.WarehouseId,
                Qty = g.Sum(e => e.QtyChange),
                Value = g.Sum(e => e.Amount),
            })
            .ToListAsync(cancellationToken);

        var balances = new List<StockBalance>(rows.Count);
        foreach (var row in rows)
        {
            balances.Add(new StockBalance(row.ItemId, row.WarehouseId, row.Qty, row.Value));
        }

        return balances;
    }

    public async Task AddStockEntryAsync(StockEntry stockEntry, CancellationToken cancellationToken = default)
    {
        await _dbContext.StockEntries.AddAsync(stockEntry, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddLedgerEntriesAsync(
        IReadOnlyList<StockLedgerEntry> ledgerEntries,
        CancellationToken cancellationToken = default)
    {
        _dbContext.StockLedgerEntries.AddRange(ledgerEntries);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddGlEntriesAsync(IReadOnlyList<GLEntry> glEntries, CancellationToken cancellationToken = default)
    {
        _dbContext.GLEntries.AddRange(glEntries);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Constitution III.4: SELECT MAX(VoucherNo) WITH (UPDLOCK, HOLDLOCK) inside the AMBIENT posting
    /// transaction, scoped to (TenantId, CompanyId, prefix-year). The range lock serializes
    /// concurrent postings per company/year, and because it lives inside the same transaction it is
    /// released by COMMIT or ROLLBACK - a rolled-back posting consumes NO number.
    /// </summary>
    public async Task<string> NextVoucherNumberAsync(
        Guid companyId,
        string prefix,
        int year,
        CancellationToken cancellationToken = default)
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
            "SELECT MAX(VoucherNo) FROM dbo.StockEntry WITH (UPDLOCK, HOLDLOCK) "
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

    public async Task<IReadOnlyList<StockEntry>> GetRecentByCompanyAsync(
        Guid companyId,
        int limit,
        CancellationToken cancellationToken = default)
        => await _dbContext.StockEntries
            .Where(e => e.CompanyId == companyId)
            .Include(e => e.Items)
            .OrderByDescending(e => e.CreatedAt)
            .ThenByDescending(e => e.Id)
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
