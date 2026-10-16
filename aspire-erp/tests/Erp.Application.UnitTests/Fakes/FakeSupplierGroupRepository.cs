using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.UnitTests.Fakes;

/// <summary>In-memory <see cref="ISupplierGroupRepository"/> over a seeded group catalog.</summary>
public sealed class FakeSupplierGroupRepository : ISupplierGroupRepository
{
    private readonly List<SupplierGroup> _groups = new();

    /// <summary>Groups visible to the service under test.</summary>
    public IReadOnlyList<SupplierGroup> Groups => _groups;

    /// <summary>Last group passed to AddAsync (null when nothing was created).</summary>
    public SupplierGroup? AddedGroup { get; private set; }

    public void Seed(params SupplierGroup[] groups) => _groups.AddRange(groups);

    public Task AddAsync(SupplierGroup group, CancellationToken cancellationToken = default)
    {
        AddedGroup = group;
        _groups.Add(group);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsCodeAsync(string code, CancellationToken cancellationToken = default)
        => Task.FromResult(_groups.Any(g => g.Code == code));

    public Task<SupplierGroup?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_groups.FirstOrDefault(g => g.Id == id));

    public Task<IReadOnlyList<SupplierGroup>> GetActiveAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<SupplierGroup>>(
            _groups.OrderByDescending(g => g.IsActive).ThenBy(g => g.Name).ToList());
}
