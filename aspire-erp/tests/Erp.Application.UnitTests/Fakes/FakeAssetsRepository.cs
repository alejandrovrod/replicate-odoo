using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.UnitTests.Fakes;

/// <summary>
/// In-memory <see cref="IAssetsRepository"/>: categories/assets come from seeded lists, the
/// posting transaction simply executes its callback, gapless AST codes come from a
/// per-(company, year) sequence, and every persisted aggregate is captured so tests can assert
/// on categories, schedule lines and GLEntry rows without a database. Mirrors
/// <see cref="FakeStockRepository"/> (added-row capture + TransactionCount for the zero-write
/// and zero-GL-leak proofs).
/// </summary>
public sealed class FakeAssetsRepository : IAssetsRepository
{
    private readonly List<AssetCategory> _categories = new();
    private readonly List<Asset> _assets = new();
    private readonly List<AssetDepreciationSchedule> _schedules = new();
    private readonly List<GLEntry> _addedGl = new();
    private readonly Dictionary<(Guid CompanyId, int Year), int> _sequences = new();

    public IReadOnlyList<AssetCategory> Categories => _categories;

    public IReadOnlyList<Asset> Assets => _assets;

    public IReadOnlyList<AssetDepreciationSchedule> AddedSchedules => _schedules;

    public IReadOnlyList<GLEntry> AddedGlEntries => _addedGl;

    /// <summary>Number of transactions opened (proves the capitalization runs inside ONE transaction).</summary>
    public int TransactionCount { get; private set; }

    public void SeedCategory(params AssetCategory[] categories) => _categories.AddRange(categories);

    public void SeedAsset(params Asset[] assets) => _assets.AddRange(assets);

    public Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        TransactionCount++;
        return operation(cancellationToken);
    }

    public Task<AssetCategory?> GetCategoryByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_categories.FirstOrDefault(c => c.Id == id));

    public Task<Asset?> GetAssetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_assets.FirstOrDefault(a => a.Id == id));

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
}
