using Erp.Domain.Common;
using Erp.Domain.Entities;

namespace Erp.Domain.Repositories;

/// <summary>
/// Data-access contract for the asset aggregates (Tasks 10.1-10.3): category and asset
/// persistence, schedule-line reads/writes, capitalization GL writes and the gapless asset-code
/// generator (Constitution III.4). Implemented by
/// Erp.Infrastructure.Data.Repositories.AssetsRepository.
/// </summary>
/// <remarks>
/// All writes happen through <see cref="ExecuteInTransactionAsync{T}"/> so the capitalization
/// voucher (asset mutation + schedule lines + GLEntry pair + asset code) commits atomically -
/// one transaction, rollback on ANY failure, zero partial postings. Tenant isolation stays
/// automatic (Constitution II.3); the only raw SQL in this repository (the asset-code MAX query)
/// scopes by TenantId itself because EF query filters do not apply to raw SQL.
/// </remarks>
public interface IAssetsRepository
{
    /// <summary>
    /// Runs <paramref name="operation"/> inside ONE database transaction and commits only when it
    /// returns; any exception rolls the whole posting back. Joins an already-open transaction so
    /// nested calls compose instead of dead-locking (same contract as IStockRepository).
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);

    /// <summary>Gets an asset category by its ID (header only).</summary>
    Task<AssetCategory?> GetCategoryByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Gets an asset by its ID (header only - lines come from GetSchedulesByAssetAsync).</summary>
    Task<Asset?> GetAssetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Lists one company's asset headers, ordered by asset code (Block B reads).</summary>
    Task<PagedResult<Asset>> GetAssetsByCompanyAsync(Guid companyId, PagedRequest paging, CancellationToken cancellationToken = default);

    /// <summary>Lists one company's asset categories, ordered by name (Block B reads).</summary>
    Task<PagedResult<AssetCategory>> GetCategoriesByCompanyAsync(Guid companyId, PagedRequest paging, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every schedule line with ScheduleDate <= <paramref name="asOfDate"/> on this company's
    /// assets, ordered by (ScheduleDate, Id) - REGARDLESS of status (Task 10.4). Booked/Cancelled
    /// rows travel with their identity so the run can skip-and-report them (spec AS-04
    /// replay safety) instead of pretending they do not exist.
    /// </summary>
    Task<IReadOnlyList<AssetDepreciationSchedule>> GetDueSchedulesAsync(
        Guid companyId, DateOnly asOfDate, CancellationToken cancellationToken = default);

    /// <summary>Gets the schedule lines of one asset, ordered by ScheduleDate.</summary>
    Task<IReadOnlyList<AssetDepreciationSchedule>> GetSchedulesByAssetAsync(Guid assetId, CancellationToken cancellationToken = default);

    /// <summary>Persists a new asset category (inside the ambient transaction when one is open).</summary>
    Task AddCategoryAsync(AssetCategory category, CancellationToken cancellationToken = default);

    /// <summary>Persists a new asset (inside the ambient transaction when one is open).</summary>
    Task AddAssetAsync(Asset asset, CancellationToken cancellationToken = default);

    /// <summary>Persists schedule lines (inside the ambient posting transaction).</summary>
    Task AddScheduleRangeAsync(IReadOnlyList<AssetDepreciationSchedule> lines, CancellationToken cancellationToken = default);

    /// <summary>Persists the General Ledger lines produced by a capitalization (inside the ambient posting transaction).</summary>
    Task AddGlEntriesAsync(IReadOnlyList<GLEntry> glEntries, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves status/value mutations of an already-tracked asset (inside the ambient transaction).
    /// Translates the RowVersion mismatch into a concurrency failure like the manufacturing
    /// repository (spec AS-06 optimistic-locking half).
    /// </summary>
    Task UpdateAssetAsync(Asset asset, CancellationToken cancellationToken = default);

    /// <summary>Saves booking/cancellation mutations of an already-tracked schedule line (Block B).</summary>
    Task UpdateScheduleAsync(AssetDepreciationSchedule line, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the disposal voucher lines for an asset by its disposal date and voucher prefix (DSP-).
    /// Used by the reversal handler to read the exact GL entries that need to be undone.
    /// </summary>
    Task<IReadOnlyList<GLEntry>> GetDisposalGlEntriesAsync(Guid assetId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Next gapless voucher number for a depreciation or disposal posting, e.g. 2026 ->
    /// "DEP-2026-00001". Constitution III.4: SELECT MAX(VoucherNo) WITH (UPDLOCK, HOLDLOCK) over
    /// dbo.GLEntry inside the AMBIENT posting transaction (the lock is released only by
    /// commit/rollback, and a rollback does not consume a number). DEP-/DSP- patterns never
    /// collide with the AST- capitalization codes (which live on the Asset row, not in GLEntry)
    /// or the JV- journal numbers sharing the same column.
    /// </summary>
    Task<string> NextVoucherNumberAsync(
        Guid companyId, string prefix, int year, CancellationToken cancellationToken = default);

    /// <summary>
    /// Next gapless reversal voucher number (prefix RDP for depreciation reversal, RDS for disposal reversal).
    /// Runs SELECT MAX(VoucherNo) WITH (UPDLOCK, HOLDLOCK) inside the AMBIENT posting transaction.
    /// </summary>
    Task<string> NextReversalVoucherNumberAsync(
        Guid companyId, string prefix, int year, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if there are existing RDS reversal GL entries for the given asset's disposal.
    /// </summary>
    Task<bool> HasDisposalReversalAsync(Guid assetId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Next gapless asset code for a company/year, e.g. 2026 -> "AST-2026-00001".
    /// Runs SELECT MAX(AssetCode) WITH (UPDLOCK, HOLDLOCK) inside the AMBIENT posting transaction
    /// (Constitution III.4: the lock is released only by commit/rollback, and a rollback does not
    /// consume a number).
    /// </summary>
    Task<string> NextAssetCodeAsync(Guid companyId, int year, CancellationToken cancellationToken = default);
}