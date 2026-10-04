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

    public Task<IReadOnlyList<StockBalance>> GetStockBalancesByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<StockBalance>>(Array.Empty<StockBalance>());

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

    public Task<IReadOnlyList<StockEntry>> GetRecentByCompanyAsync(Guid companyId, int limit, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<StockEntry>>(_stockEntries.TakeLast(limit).Reverse().ToList());

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

