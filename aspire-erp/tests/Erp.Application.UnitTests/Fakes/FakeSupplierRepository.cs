using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.UnitTests.Fakes;

/// <summary>In-memory <see cref="ISupplierRepository"/> over a seeded supplier set (Task 4.1).</summary>
public sealed class FakeSupplierRepository : ISupplierRepository
{
    private readonly List<Supplier> _suppliers = new();

    /// <summary>Suppliers visible to the service under test.</summary>
    public IReadOnlyList<Supplier> Suppliers => _suppliers;

    /// <summary>Last supplier passed to AddAsync (null when nothing was created).</summary>
    public Supplier? AddedSupplier { get; private set; }

    public void Seed(params Supplier[] suppliers) => _suppliers.AddRange(suppliers);

    public Task AddAsync(Supplier supplier, CancellationToken cancellationToken = default)
    {
        AddedSupplier = supplier;
        _suppliers.Add(supplier);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsCodeAsync(string code, CancellationToken cancellationToken = default)
        => Task.FromResult(_suppliers.Any(s => s.Code == code));

    public Task<Supplier?> GetByIdAsync(Guid supplierId, CancellationToken cancellationToken = default)
        => Task.FromResult(_suppliers.FirstOrDefault(s => s.Id == supplierId));

    public Task<IReadOnlyList<Supplier>> GetByIdsAsync(
        IReadOnlyList<Guid> supplierIds,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Supplier>>(
            _suppliers.Where(s => supplierIds.Contains(s.Id)).ToList());

    public Task<IReadOnlyList<Supplier>> GetRecentAsync(int limit, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Supplier>>(
            _suppliers
                .OrderByDescending(s => s.CreatedAt)
                .ThenByDescending(s => s.Id)
                .Take(limit)
                .ToList());
}

