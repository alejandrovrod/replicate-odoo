using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.UnitTests.Fakes;

/// <summary>
/// In-memory <see cref="IAssetsRepository"/>: categories/assets come from seeded lists, the
/// posting transaction simply executes its callback, gapless AST codes come from a
/// per-(company, year) sequence, depreciation/disposal vouchers (DEP-/DSP-) come from a
/// per-(company, prefix, year) sequence, and every persisted aggregate is captured so tests can
/// assert on categories, schedule lines and GLEntry rows without a database. Mirrors
/// <see cref="FakeStockRepository"/> (added-row capture + TransactionCount for the zero-write
/// and zero-GL-leak proofs).
/// </summary>
/// <remarks>
/// Rollback simulation (the FakePurchaseRepository snapshot pattern): the transaction callback
/// mutates shared in-memory instances eagerly, so on ANY exception the pre-call snapshot -
/// asset fields, schedule statuses, added-row counts - is restored before rethrowing, exactly
/// like the real repository abandoning its uncommitted transaction. Optimistic-concurrency
/// races are simulated with <see cref="FailNextAssetUpdate"/> (the next asset save throws
/// <see cref="ConcurrencyConflictException"/> like the real repository does after a RowVersion
/// mismatch, then the flag resets so only one call fails).
/// </remarks>
public sealed class FakeAssetsRepository : IAssetsRepository
{
    private readonly List<AssetCategory> _categories = new();
    private readonly List<Asset> _assets = new();
    private readonly List<AssetDepreciationSchedule> _schedules = new();
    private readonly List<GLEntry> _addedGl = new();
    private readonly Dictionary<(Guid CompanyId, int Year), int> _sequences = new();
    private readonly Dictionary<(Guid CompanyId, string Prefix, int Year), int> _voucherSequences = new();

    public IReadOnlyList<AssetCategory> Categories => _categories;

    public IReadOnlyList<Asset> Assets => _assets;

    public IReadOnlyList<AssetDepreciationSchedule> AddedSchedules => _schedules;

    public IReadOnlyList<GLEntry> AddedGlEntries => _addedGl;

    /// <summary>Number of transactions opened (proves each posting runs inside ONE transaction).</summary>
    public int TransactionCount { get; private set; }

    /// <summary>
    /// When set, the NEXT <c>UpdateAssetAsync</c> fails like the real repository does after a
    /// RowVersion mismatch; the flag resets itself so only one call fails.
    /// </summary>
    public bool FailNextAssetUpdate { get; set; }

    public void SeedCategory(params AssetCategory[] categories) => _categories.AddRange(categories);

    public void SeedAsset(params Asset[] assets) => _assets.AddRange(assets);

    public void SeedSchedule(params AssetDepreciationSchedule[] schedules) => _schedules.AddRange(schedules);

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        TransactionCount++;

        // The real repository rolls the whole transaction back when the operation throws; the
        // in-memory fake models that for the mutations the 10.4/10.5 handlers perform BEFORE a
        // gate or CAS failure (asset fields, schedule statuses, added GL/schedule rows), so
        // rejection tests can assert the books are untouched.
        var assetSnapshot = _assets
            .Select(a => (Asset: a, a.Status, a.AccumulatedDepreciation, a.DisposalDate, a.AssetCode))
            .ToList();
        var scheduleSnapshot = _schedules
            .Select(s => (Line: s, s.Status, s.JournalEntryId))
            .ToList();
        var scheduleCount = _schedules.Count;
        var glCount = _addedGl.Count;

        try
        {
            return await operation(cancellationToken);
        }
        catch
        {
            foreach (var (asset, status, accumulated, disposalDate, assetCode) in assetSnapshot)
            {
                asset.Status = status;
                asset.AccumulatedDepreciation = accumulated;
                asset.DisposalDate = disposalDate;
                asset.AssetCode = assetCode;
            }

            foreach (var (line, status, journalEntryId) in scheduleSnapshot)
            {
                line.Status = status;
                line.JournalEntryId = journalEntryId;
            }

            while (_schedules.Count > scheduleCount)
            {
                _schedules.RemoveAt(_schedules.Count - 1);
            }

            while (_addedGl.Count > glCount)
            {
                _addedGl.RemoveAt(_addedGl.Count - 1);
            }

            throw;
        }
    }

    public Task<AssetCategory?> GetCategoryByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_categories.FirstOrDefault(c => c.Id == id));

    public Task<Asset?> GetAssetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_assets.FirstOrDefault(a => a.Id == id));

    public Task<IReadOnlyList<Asset>> GetAssetsByCompanyAsync(
        Guid companyId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Asset>>(
            _assets.Where(a => a.CompanyId == companyId).OrderBy(a => a.AssetCode).ThenBy(a => a.Id).ToList());

    public Task<IReadOnlyList<AssetCategory>> GetCategoriesByCompanyAsync(
        Guid companyId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<AssetCategory>>(
            _categories.Where(c => c.CompanyId == companyId).OrderBy(c => c.CategoryName).ThenBy(c => c.Id).ToList());

    public Task<IReadOnlyList<AssetDepreciationSchedule>> GetDueSchedulesAsync(
        Guid companyId,
        DateOnly asOfDate,
        CancellationToken cancellationToken = default)
    {
        var assetCompany = _assets.ToDictionary(a => a.Id, a => a.CompanyId);
        return Task.FromResult<IReadOnlyList<AssetDepreciationSchedule>>(
            _schedules
                .Where(s => s.ScheduleDate <= asOfDate
                    && assetCompany.TryGetValue(s.AssetId, out var owner)
                    && owner == companyId)
                .OrderBy(s => s.ScheduleDate)
                .ThenBy(s => s.Id)
                .ToList());
    }

    public Task<IReadOnlyList<AssetDepreciationSchedule>> GetSchedulesByAssetAsync(
        Guid assetId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<AssetDepreciationSchedule>>(
            _schedules.Where(s => s.AssetId == assetId).OrderBy(s => s.ScheduleDate).ToList());

    public Task AddCategoryAsync(AssetCategory category, CancellationToken cancellationToken = default)
    {
        _categories.Add(category);
        return Task.CompletedTask;
    }

    public Task AddAssetAsync(Asset asset, CancellationToken cancellationToken = default)
    {
        _assets.Add(asset);
        return Task.CompletedTask;
    }

    public Task AddScheduleRangeAsync(
        IReadOnlyList<AssetDepreciationSchedule> lines,
        CancellationToken cancellationToken = default)
    {
        _schedules.AddRange(lines);
        return Task.CompletedTask;
    }

    public Task AddGlEntriesAsync(IReadOnlyList<GLEntry> glEntries, CancellationToken cancellationToken = default)
    {
        _addedGl.AddRange(glEntries);
        return Task.CompletedTask;
    }

    public Task UpdateAssetAsync(Asset asset, CancellationToken cancellationToken = default)
    {
        if (FailNextAssetUpdate)
        {
            FailNextAssetUpdate = false;
            throw new ConcurrencyConflictException(nameof(Asset), asset.Id);
        }

        // In-memory: the entity instance IS the store; status/code mutations are already applied.
        return Task.CompletedTask;
    }

    public Task UpdateScheduleAsync(AssetDepreciationSchedule line, CancellationToken cancellationToken = default)
    {
        // In-memory: the entity instance IS the store; status mutations are already applied.
        return Task.CompletedTask;
    }

    public Task<string> NextAssetCodeAsync(Guid companyId, int year, CancellationToken cancellationToken = default)
    {
        var key = (companyId, year);
        _sequences.TryGetValue(key, out var current);
        _sequences[key] = current + 1;
        return Task.FromResult($"AST-{year}-{current + 1:D5}");
    }

    public Task<string> NextVoucherNumberAsync(
        Guid companyId,
        string prefix,
        int year,
        CancellationToken cancellationToken = default)
    {
        var key = (companyId, prefix, year);
        _voucherSequences.TryGetValue(key, out var current);
        _voucherSequences[key] = current + 1;
        return Task.FromResult($"{prefix}-{year}-{current + 1:D5}");
    }
}
