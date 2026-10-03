using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.UnitTests.Fakes;

/// <summary>In-memory <see cref="IUomRepository"/> over a seeded unit catalog.</summary>
public sealed class FakeUomRepository : IUomRepository
{
    private readonly List<UOM> _uoms = new();

    public IReadOnlyList<UOM> Uoms => _uoms;

    public void Seed(params UOM[] uoms) => _uoms.AddRange(uoms);

    public Task<UOM?> GetByIdAsync(Guid uomId, CancellationToken cancellationToken = default)
        => Task.FromResult(_uoms.FirstOrDefault(u => u.Id == uomId));
}

