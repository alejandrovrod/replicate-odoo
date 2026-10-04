using Erp.Domain.Entities;
using Erp.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IIdempotencyRepository"/> (decision D7). The reservation
/// insert relies on the UNIQUE (TenantId, Key) index: a duplicate key makes SaveChanges throw
/// <see cref="DbUpdateException"/>, which is converted into "reserved = false" so the caller can
/// classify the winning record instead. The implicit SaveChanges transaction IS the "own short
/// transaction" the reservation needs.
/// </summary>
public sealed class IdempotencyRepository : IIdempotencyRepository
{
    private readonly AppDbContext _dbContext;

    public IdempotencyRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<IdempotencyRecord?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
        => _dbContext.IdempotencyRecords.FirstOrDefaultAsync(r => r.Key == key, cancellationToken);

    public async Task<bool> TryReserveAsync(string key, string requestHash, CancellationToken cancellationToken = default)
    {
        var record = new IdempotencyRecord
        {
            Id = Guid.NewGuid(),
            Key = key,
            RequestHash = requestHash,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _dbContext.IdempotencyRecords.Add(record);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            // Unique (TenantId, Key) violation: another request reserved this key first. Detach the
            // losing entry so the context stays queryable, then let the caller read the winner.
            _dbContext.Entry(record).State = EntityState.Detached;
            return false;
        }
    }

    public async Task CompleteAsync(
        string key,
        int responseStatus,
        string responseBody,
        CancellationToken cancellationToken = default)
    {
        var record = await _dbContext.IdempotencyRecords.FirstOrDefaultAsync(r => r.Key == key, cancellationToken);
        if (record is null)
        {
            return;
        }

        record.ResponseStatus = responseStatus;
        record.ResponseBody = responseBody;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ReleaseAsync(string key, CancellationToken cancellationToken = default)
    {
        var record = await _dbContext.IdempotencyRecords.FirstOrDefaultAsync(r => r.Key == key, cancellationToken);
        if (record is null)
        {
            return;
        }

        _dbContext.IdempotencyRecords.Remove(record);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // The action failed on a RowVersion race AFTER mutating tracked business entities
            // (the BN-07 save race: the handler translated it into a clean 409, but the dead
            // entities stay in the tracker). Retrying them inside THIS save tears the
            // already-written 409 with a raw 500. Their transaction already rolled back, so
            // detach them and retry the release alone (the TryReserveAsync precedent above).
            foreach (var entry in ex.Entries)
            {
                entry.State = EntityState.Detached;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
