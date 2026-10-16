using Erp.Domain.Entities;
using Erp.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ITenantPresetRepository"/> (Fase 5).
/// No manual tenant predicates (Constitution II.3 - the global query filter scopes reads,
/// so "the tenant's record" is simply the single row visible here).
/// </summary>
public sealed class TenantPresetRepository : ITenantPresetRepository
{
    private readonly AppDbContext _dbContext;

    public TenantPresetRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<TenantPreset?> GetAsync(CancellationToken cancellationToken = default)
        => _dbContext.TenantPresets.FirstOrDefaultAsync(cancellationToken);

    public async Task SetAsync(string presetKey, CancellationToken cancellationToken = default)
    {
        var record = await _dbContext.TenantPresets.FirstOrDefaultAsync(cancellationToken);
        if (record is null)
        {
            await _dbContext.TenantPresets.AddAsync(new TenantPreset
            {
                Id = Guid.NewGuid(),
                PresetKey = presetKey,
                AppliedAt = DateTimeOffset.UtcNow,
            }, cancellationToken);
        }
        else
        {
            record.PresetKey = presetKey;
            record.AppliedAt = DateTimeOffset.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
