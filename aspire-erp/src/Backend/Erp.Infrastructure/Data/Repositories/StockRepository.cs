using System.Data;
using System.Data.Common;
using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;
using Erp.Infrastructure.Data.Pagination;
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
public sealed class StockRepository : IStockRepository, IStockLedgerReportRepository
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

    /// <summary>
    /// Task 3.9: SELECT ... WITH (UPDLOCK, HOLDLOCK) over the Kardex rows of the given (item,
    /// warehouse) pairs inside the AMBIENT posting transaction. UPDLOCK makes competing consumers
    /// queue on this range instead of taking shared locks, and HOLDLOCK (serializable) keeps the
    /// range locked - including the gaps of an empty result set - until COMMIT or ROLLBACK. The
    /// statement therefore runs BEFORE any FIFO layer is read, and because it lives in the same
    /// transaction as the posting, a rollback releases it without consuming anything.
    /// </summary>
    /// <remarks>
    /// EF's global query filter does NOT apply to raw SQL, so the tenant scope is written out
    /// explicitly (same contract as <see cref="NextVoucherNumberAsync"/>). The key-range locks
    /// follow the IX_StockLedger_Tenant_Item_Warehouse_Date index seek in the SAME order for every
    /// transaction, which makes the acquisition deadlock-free by construction.
    /// </remarks>
    public async Task LockStockRangeAsync(
        IReadOnlyCollection<Guid> itemIds,
        IReadOnlyCollection<Guid> warehouseIds,
        CancellationToken cancellationToken = default)
    {
        if (itemIds.Count == 0 || warehouseIds.Count == 0)
        {
            return;
        }

        var transaction = _dbContext.Database.CurrentTransaction
            ?? throw new InvalidOperationException(
                "The stock range lock must run inside the posting transaction (Task 3.9): outside "
                + "one the UPDLOCK/HOLDLOCK would release at statement end and protect nothing.");

        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await _dbContext.Database.OpenConnectionAsync(cancellationToken);
        }

        var itemNames = new List<string>(itemIds.Count);
        var warehouseNames = new List<string>(warehouseIds.Count);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction.GetDbTransaction();

        var index = 0;
        foreach (var itemId in itemIds)
        {
            var name = $"@Item{index++}";
            itemNames.Add(name);
            AddParameter(command, name, itemId);
        }

        index = 0;
        foreach (var warehouseId in warehouseIds)
        {
            var name = $"@Warehouse{index++}";
            warehouseNames.Add(name);
            AddParameter(command, name, warehouseId);
        }

        command.CommandText =
            "SELECT Id FROM dbo.StockLedgerEntry WITH (UPDLOCK, HOLDLOCK) "
            + "WHERE TenantId = @TenantId AND ItemId IN ("
            + string.Join(", ", itemNames)
            + ") AND WarehouseId IN ("
            + string.Join(", ", warehouseNames)
            + ");";
        AddParameter(command, "@TenantId", _dbContext.CurrentTenantId);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

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

    public async Task<StockSummary> GetStockSummaryAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        // Scalar aggregates only: five cheap COUNT/SUM round-trips, zero materialized rows.
        var warehouseIds = _dbContext.Warehouses.Where(w => w.CompanyId == companyId);

        var totalSkus = await _dbContext.Items.CountAsync(cancellationToken);
        var activeSkus = await _dbContext.Items.CountAsync(i => i.IsActive, cancellationToken);
        var totalValue = await _dbContext.StockLedgerEntries
            .Where(e => warehouseIds.Select(w => w.Id).Contains(e.WarehouseId))
            .SumAsync(e => (decimal?)e.Amount, cancellationToken) ?? 0m;
        var warehouseCount = await warehouseIds.CountAsync(cancellationToken);
        var leafCount = await warehouseIds.CountAsync(w => !w.IsGroup, cancellationToken);

        return new StockSummary(totalSkus, activeSkus, totalValue, warehouseCount, leafCount);
    }

    public async Task AddStockEntryAsync(StockEntry stockEntry, CancellationToken cancellationToken = default)
    {
        await _dbContext.StockEntries.AddAsync(stockEntry, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateStockEntryAsync(StockEntry stockEntry, CancellationToken cancellationToken = default)
    {
        _dbContext.StockEntries.Update(stockEntry);
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

    public async Task<PagedResult<StockEntry>> GetRecentByCompanyAsync(
        Guid companyId,
        PagedRequest paging,
        CancellationToken cancellationToken = default)
        => await _dbContext.StockEntries
            .Where(e => e.CompanyId == companyId)
            .Include(e => e.Items)
            .OrderByDescending(e => e.CreatedAt)
            .ThenByDescending(e => e.Id)
            .ToPagedResultAsync(paging, cancellationToken);

    public async Task<StockEntry?> GetEntryByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _dbContext.StockEntries
            .Include(e => e.Items)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<IReadOnlyList<StockLedgerEntry>> GetLedgerEntriesByVoucherAsync(string voucherNo, CancellationToken cancellationToken = default)
        => await _dbContext.StockLedgerEntries
            .Where(e => e.VoucherNo == voucherNo)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<StockLedgerEntry>> GetLedgerEntriesByCompanyAsync(
        Guid companyId,
        DateOnly from,
        DateOnly to,
        Guid? itemId,
        Guid? warehouseId,
        int take,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.StockLedgerEntries
            .Where(e => e.Warehouse!.CompanyId == companyId && e.PostingDate >= from && e.PostingDate <= to);

        if (itemId.HasValue)
        {
            query = query.Where(e => e.ItemId == itemId.Value);
        }

        if (warehouseId.HasValue)
        {
            query = query.Where(e => e.WarehouseId == warehouseId.Value);
        }

        return await query
            .Include(e => e.Item)
            .Include(e => e.Warehouse)
            .OrderBy(e => e.PostingDate)
            .ThenBy(e => e.CreatedAt)
            .ThenBy(e => e.Id)
            .Take(Math.Clamp(take, 1, 5000))
            .ToListAsync(cancellationToken);
    }

    public async Task<(decimal Qty, decimal Value)> GetOpeningBalanceAsync(
        Guid itemId,
        Guid warehouseId,
        DateOnly from,
        CancellationToken cancellationToken = default)
    {
        var rows = await _dbContext.StockLedgerEntries
            .Where(e => e.ItemId == itemId && e.WarehouseId == warehouseId && e.PostingDate < from)
            .Select(e => new { e.QtyChange, e.Amount })
            .ToListAsync(cancellationToken);

        return (rows.Sum(r => r.QtyChange), rows.Sum(r => r.Amount));
    }

    public async Task<IReadOnlyList<GLEntry>> GetGlEntriesByVoucherIdAsync(Guid voucherId, CancellationToken cancellationToken = default)
        => await _dbContext.GLEntries
            .Where(e => e.VoucherId == voucherId)
            .ToListAsync(cancellationToken);

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
