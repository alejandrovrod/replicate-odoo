using Erp.Domain.Entities;
using Erp.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IBatchRepository"/> (batch-and-serial Fase 3).
/// No manual tenant predicates (Constitution II.3 - the global query filter scopes reads).
/// </summary>
public sealed class BatchRepository : IBatchRepository
{
    private readonly AppDbContext _dbContext;

    public BatchRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Batch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.Batches.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

    public Task<Batch?> GetByItemAndNumberAsync(Guid itemId, string batchNo, CancellationToken cancellationToken = default)
        => _dbContext.Batches.FirstOrDefaultAsync(
            b => b.ItemId == itemId && b.BatchId == batchNo, cancellationToken);

    public async Task<IReadOnlyList<Batch>> GetByItemAsync(Guid itemId, CancellationToken cancellationToken = default)
        => await _dbContext.Batches
            .Where(b => b.ItemId == itemId)
            .OrderBy(b => b.BatchId)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Batch batch, CancellationToken cancellationToken = default)
    {
        await _dbContext.Batches.AddAsync(batch, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Batch batch, CancellationToken cancellationToken = default)
    {
        _dbContext.Batches.Update(batch);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
