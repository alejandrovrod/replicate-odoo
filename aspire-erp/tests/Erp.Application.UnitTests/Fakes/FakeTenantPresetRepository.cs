using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.UnitTests.Fakes;

/// <summary>In-memory <see cref="ITenantPresetRepository"/> holding at most one record.</summary>
public sealed class FakeTenantPresetRepository : ITenantPresetRepository
{
    private TenantPreset? _record;

    public TenantPreset? Record => _record;

    public Task<TenantPreset?> GetAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_record);

    public Task SetAsync(string presetKey, CancellationToken cancellationToken = default)
    {
        if (_record is null)
        {
            _record = new TenantPreset
            {
                Id = Guid.NewGuid(),
                PresetKey = presetKey,
                AppliedAt = DateTimeOffset.UtcNow,
            };
        }
        else
        {
            _record.PresetKey = presetKey;
            _record.AppliedAt = DateTimeOffset.UtcNow;
        }

        return Task.CompletedTask;
    }
}
