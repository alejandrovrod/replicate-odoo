using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.UnitTests.Fakes;

/// <summary>In-memory <see cref="IBatchRepository"/> over a seeded batch set.</summary>
public sealed class FakeBatchRepository : IBatchRepository
{
    private readonly List<Batch> _batches = new();

    public IReadOnlyList<Batch> Batches => _batches;

    public void Seed(params Batch[] batches) => _batches.AddRange(batches);

    public Task<Batch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_batches.FirstOrDefault(b => b.Id == id));

    public Task<Batch?> GetByItemAndNumberAsync(Guid itemId, string batchNo, CancellationToken cancellationToken = default)
        => Task.FromResult(_batches.FirstOrDefault(b => b.ItemId == itemId && b.BatchId == batchNo));

    public Task<IReadOnlyList<Batch>> GetByItemAsync(Guid itemId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Batch>>(_batches.Where(b => b.ItemId == itemId).ToList());

    public Task AddAsync(Batch batch, CancellationToken cancellationToken = default)
    {
        _batches.Add(batch);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Batch batch, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
