using Erp.Domain.Common;
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

    public Task UpdateAsync(Supplier supplier, CancellationToken cancellationToken = default)
    {
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

    // In-memory fakes ignore paging and return the whole seeded set (see FakeCustomerRepository),
    // but the module buying-suppliers filters are honored so the query handler can be exercised.
    public Task<PagedResult<Supplier>> GetRecentAsync(
        PagedRequest paging,
        Guid? supplierGroupId = null,
        bool? onHold = null,
        Guid? companyId = null,
        CancellationToken cancellationToken = default)
    {
        var filtered = _suppliers.AsEnumerable();

        if (supplierGroupId.HasValue)
        {
            filtered = filtered.Where(s => s.SupplierGroupId == supplierGroupId.Value);
        }

        if (onHold.HasValue)
        {
            filtered = filtered.Where(s => s.OnHold == onHold.Value);
        }

        if (companyId.HasValue)
        {
            filtered = filtered.Where(s => s.Accounts.Any(a => a.CompanyId == companyId.Value));
        }

        var items = filtered
            .OrderByDescending(s => s.CreatedAt)
            .ThenByDescending(s => s.Id)
            .ToList();

        return Task.FromResult(
            new PagedResult<Supplier>(items, items.Count, paging.SafePageNumber, paging.SafePageSize));
    }
}

