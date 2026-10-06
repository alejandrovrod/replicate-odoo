using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.UnitTests.Fakes;

/// <summary>In-memory <see cref="IWarehouseRepository"/> over a seeded set of warehouses.</summary>
public sealed class FakeWarehouseRepository : IWarehouseRepository
{
    private readonly List<Warehouse> _warehouses = new();

    public IReadOnlyList<Warehouse> Warehouses => _warehouses;

    public Warehouse? AddedWarehouse { get; private set; }

    public IReadOnlyList<Warehouse> Ancestors { get; set; } = Array.Empty<Warehouse>();

    public void Seed(params Warehouse[] warehouses) => _warehouses.AddRange(warehouses);

    public Task AddAsync(Warehouse warehouse, CancellationToken cancellationToken = default)
    {
        AddedWarehouse = warehouse;
        _warehouses.Add(warehouse);
        return Task.CompletedTask;
    }

    public Task<Warehouse?> GetByIdAsync(Guid warehouseId, CancellationToken cancellationToken = default)
        => Task.FromResult(_warehouses.FirstOrDefault(w => w.Id == warehouseId));

    public Task<IReadOnlyList<Warehouse>> GetByIdWithAncestorsAsync(Guid warehouseId, CancellationToken cancellationToken = default)
        => Task.FromResult(Ancestors);

    public Task<IReadOnlyList<Warehouse>> GetByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Warehouse>>(_warehouses.Where(w => w.CompanyId == companyId).ToList());

    // In-memory fakes ignore paging and return the whole seeded set (see FakeCustomerRepository).
    public Task<PagedResult<Warehouse>> GetFlatWarehousesAsync(
        Guid companyId,
        bool leavesOnly,
        bool? isActive,
        PagedRequest paging,
        CancellationToken cancellationToken = default)
    {
        var items = _warehouses.Where(w => w.CompanyId == companyId);
        if (leavesOnly)
        {
            items = items.Where(w => !w.IsGroup);
        }

        if (isActive is not null)
        {
            items = items.Where(w => w.IsActive == isActive);
        }

        var list = items.OrderBy(w => w.WarehouseCode).ToList();
        return Task.FromResult(new PagedResult<Warehouse>(list, list.Count, paging.SafePageNumber, paging.SafePageSize));
    }

    public Task<bool> ExistsByCodeAsync(Guid companyId, string code, CancellationToken cancellationToken = default)
        => Task.FromResult(_warehouses.Any(w => w.CompanyId == companyId && w.WarehouseCode == code));

    public Task UpdateAsync(Warehouse warehouse, CancellationToken cancellationToken = default)
    {
        UpdatedWarehouse = warehouse;
        return Task.CompletedTask;
    }

    public Warehouse? UpdatedWarehouse { get; private set; }
}



