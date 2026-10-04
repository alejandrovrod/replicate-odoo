using Erp.Domain.Entities;

namespace Erp.Domain.Repositories;

/// <summary>
/// Data-access contract for the perpetual inventory engine (Task 3.2 / decision D8): FIFO layer
/// reads, stock balances, stock voucher persistence, General Ledger writes and the gapless voucher
/// number generator (Constitution III.4). Implemented by
/// Erp.Infrastructure.Data.Repositories.StockRepository.
/// </summary>
/// <remarks>
/// All writes happen through <see cref="ExecuteInTransactionAsync{T}"/> so validation, FIFO,
/// StockLedgerEntry rows, GLEntry rows and the voucher number commit atomically (one transaction,
/// rollback on ANY failure - zero partial postings). Tenant isolation stays automatic
/// (Constitution II.3); the only raw SQL in this repository (the voucher MAX query) scopes by
/// TenantId itself because EF query filters do not apply to raw SQL.
/// </remarks>
public interface IStockRepository
{
    /// <summary>
    /// Runs <paramref name="operation"/> inside ONE database transaction and commits only when it
    /// returns; any exception rolls the whole posting back. Joins an already-open transaction so
    /// nested calls compose instead of dead-locking.
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Signed Kardex rows for one (item, warehouse) pair with <c>PostingDate &lt;= asOf</c>,
    /// ordered chronologically (PostingDate, CreatedAt, Id) - the raw material of the FIFO engine.
    /// </summary>
    Task<IReadOnlyList<StockLedgerEntry>> GetFifoLayersAsync(
        Guid itemId,
        Guid warehouseId,
        DateOnly asOf,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Task 3.9 (overselling prevention): takes <c>UPDLOCK, HOLDLOCK</c> key locks over the
    /// EXISTING Kardex rows of every (item, warehouse) pair in the AMBIENT posting transaction,
    /// BEFORE any FIFO layer is read. A second consumer of the same pairs blocks here until this
    /// transaction commits or rolls back and then re-reads the committed layers, so two concurrent
    /// issues can never consume the same units (a plain SELECT read cannot close that race).
    /// WarehouseIds includes BOTH source and target for a transfer, because a transfer also
    /// inserts into its target range.
    /// </summary>
    /// <remarks>
    /// Locks are taken in the order dictated by the (TenantId, ItemId, WarehouseId, PostingDate)
    /// index seek for every caller - a globally consistent acquisition order that cannot
    /// deadlock. Receipts only append rows and never call this.
    /// </remarks>
    Task LockStockRangeAsync(
        IReadOnlyCollection<Guid> itemIds,
        IReadOnlyCollection<Guid> warehouseIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// On-hand quantity and value per (item, warehouse) for every warehouse of a company - the
    /// source of the ItemList stock levels (Task 3.4 consumes it through GetItemsQuery).
    /// </summary>
    Task<IReadOnlyList<StockBalance>> GetStockBalancesByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default);

    /// <summary>Persists one stock voucher with its lines (inside the ambient posting transaction).</summary>
    Task AddStockEntryAsync(StockEntry stockEntry, CancellationToken cancellationToken = default);

    Task UpdateStockEntryAsync(StockEntry stockEntry, CancellationToken cancellationToken = default);

    /// <summary>Persists the Kardex rows produced by a voucher (inside the ambient posting transaction).</summary>
    Task AddLedgerEntriesAsync(IReadOnlyList<StockLedgerEntry> ledgerEntries, CancellationToken cancellationToken = default);

    /// <summary>Persists the General Ledger lines produced by a voucher (inside the ambient posting transaction).</summary>
    Task AddGlEntriesAsync(IReadOnlyList<GLEntry> glEntries, CancellationToken cancellationToken = default);

    /// <summary>
    /// Next gapless number for a company/prefix/year, e.g. prefix "MR" + 2026 -> "MR-2026-00001".
    /// Runs SELECT MAX(VoucherNo) WITH (UPDLOCK, HOLDLOCK) inside the AMBIENT posting transaction
    /// (Constitution III.4: the lock is released only by commit/rollback, and a rollback does not
    /// consume a number).
    /// </summary>
    Task<string> NextVoucherNumberAsync(Guid companyId, string prefix, int year, CancellationToken cancellationToken = default);

    /// <summary>Most recent stock vouchers of a company (newest first) for the list view.</summary>
    Task<IReadOnlyList<StockEntry>> GetRecentByCompanyAsync(Guid companyId, int limit, CancellationToken cancellationToken = default);

    /// <summary>Gets a stock entry by its ID, including its Items.</summary>
    Task<StockEntry?> GetEntryByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Gets the Kardex rows produced by a voucher number.</summary>
    Task<IReadOnlyList<StockLedgerEntry>> GetLedgerEntriesByVoucherAsync(string voucherNo, CancellationToken cancellationToken = default);

    /// <summary>Gets the GL rows produced by a voucher ID.</summary>
    Task<IReadOnlyList<GLEntry>> GetGlEntriesByVoucherIdAsync(Guid voucherId, CancellationToken cancellationToken = default);
}

/// <summary>Read-model row for one (item, warehouse) pair inside a company.</summary>
public sealed record StockBalance(Guid ItemId, Guid WarehouseId, decimal Qty, decimal Value);
