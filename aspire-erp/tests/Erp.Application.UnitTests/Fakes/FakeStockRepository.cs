using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.UnitTests.Fakes;

/// <summary>
/// In-memory <see cref="IStockRepository"/>: FIFO layers come from a seeded Kardex list, the
/// posting transaction simply executes its callback, and every persisted aggregate is captured so
/// tests can assert on StockEntry / StockLedgerEntry / GLEntry rows without a database.
/// </summary>
public sealed class FakeStockRepository : IStockRepository
{
    private readonly List<StockLedgerEntry> _persistedLedger = new();
    private readonly List<StockEntry> _stockEntries = new();
    private readonly List<StockLedgerEntry> _addedLedger = new();
    private readonly List<GLEntry> _addedGl = new();
    private readonly Dictionary<(Guid CompanyId, string Prefix, int Year), int> _sequences = new();

    /// <summary>Kardex rows already on disk (the raw material of the FIFO layer rebuild).</summary>
    public IReadOnlyList<StockLedgerEntry> PersistedLedger => _persistedLedger;

    public IReadOnlyList<StockEntry> StockEntries => _stockEntries;

    public IReadOnlyList<StockLedgerEntry> AddedLedger => _addedLedger;

    public IReadOnlyList<GLEntry> AddedGlEntries => _addedGl;

    /// <summary>Number of transactions opened (proves the posting runs inside ONE transaction).</summary>
    public int TransactionCount { get; private set; }

    public void SeedLedger(params StockLedgerEntry[] entries) => _persistedLedger.AddRange(entries);

    public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        TransactionCount++;
        return operation(cancellationToken);
    }

    public Task<IReadOnlyList<StockLedgerEntry>> GetFifoLayersAsync(
        Guid itemId,
        Guid warehouseId,
        DateOnly asOf,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<StockLedgerEntry>>(
            _persistedLedger
                .Where(e => e.ItemId == itemId && e.WarehouseId == warehouseId && e.PostingDate <= asOf)
                .OrderBy(e => e.PostingDate)
                .ThenBy(e => e.CreatedAt)
                .ToList());

    /// <summary>Range-lock requests the posting service asked for, in order (Task 3.9 call-site assertions).</summary>
    public List<(IReadOnlyCollection<Guid> ItemIds, IReadOnlyCollection<Guid> WarehouseIds)> StockRangeLocks { get; } = new();

    public Task LockStockRangeAsync(
        IReadOnlyCollection<Guid> itemIds,
        IReadOnlyCollection<Guid> warehouseIds,
        CancellationToken cancellationToken = default)
    {
        // The in-memory fake needs no real lock (there is no database to race on): capture the
        // request so tests can assert the service takes it for consumers and skips it for receipts.
        StockRangeLocks.Add((itemIds, warehouseIds));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<StockBalance>> GetStockBalancesByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<StockBalance>>(Array.Empty<StockBalance>());

    public Task<StockSummary> GetStockSummaryAsync(Guid companyId, CancellationToken cancellationToken = default)
        => Task.FromResult(new StockSummary(0, 0, 0m, 0, 0));

    public Task AddStockEntryAsync(StockEntry stockEntry, CancellationToken cancellationToken = default)
    {
        _stockEntries.Add(stockEntry);
        return Task.CompletedTask;
    }

    public Task AddLedgerEntriesAsync(IReadOnlyList<StockLedgerEntry> ledgerEntries, CancellationToken cancellationToken = default)
    {
        _addedLedger.AddRange(ledgerEntries);
        _persistedLedger.AddRange(ledgerEntries);
        return Task.CompletedTask;
    }

    public Task AddGlEntriesAsync(IReadOnlyList<GLEntry> glEntries, CancellationToken cancellationToken = default)
    {
        _addedGl.AddRange(glEntries);
        return Task.CompletedTask;
    }

    public Task<string> NextVoucherNumberAsync(Guid companyId, string prefix, int year, CancellationToken cancellationToken = default)
    {
        var key = (companyId, prefix, year);
        var next = _sequences.TryGetValue(key, out var current) ? current + 1 : 1;
        _sequences[key] = next;
        return Task.FromResult($"{prefix}-{year}-{next:00000}");
    }

    // In-memory fakes ignore paging and return the whole seeded set (see FakeCustomerRepository).
    public Task<PagedResult<StockEntry>> GetRecentByCompanyAsync(Guid companyId, PagedRequest paging, CancellationToken cancellationToken = default)
    {
        var items = _stockEntries.AsEnumerable().Reverse().ToList();
        return Task.FromResult(new PagedResult<StockEntry>(items, items.Count, paging.SafePageNumber, paging.SafePageSize));
    }

    public Task UpdateStockEntryAsync(StockEntry stockEntry, CancellationToken cancellationToken = default)
    {
        var index = _stockEntries.FindIndex(e => e.Id == stockEntry.Id);
        if (index >= 0)
        {
            _stockEntries[index] = stockEntry;
        }
        else
        {
            _stockEntries.Add(stockEntry);
        }

        return Task.CompletedTask;
    }

    public Task<StockEntry?> GetEntryByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_stockEntries.FirstOrDefault(e => e.Id == id));

    public Task<IReadOnlyList<StockLedgerEntry>> GetLedgerEntriesByVoucherAsync(string voucherNo, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<StockLedgerEntry>>(
            _persistedLedger.Where(e => e.VoucherNo == voucherNo).ToList());

    public Task<IReadOnlyList<GLEntry>> GetGlEntriesByVoucherIdAsync(Guid voucherId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<GLEntry>>(
            _addedGl.Where(g => g.VoucherId == voucherId).ToList());
}

