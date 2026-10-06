using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.UnitTests.Fakes;

/// <summary>
/// In-memory <see cref="IManufacturingRepository"/>: gapless WO numbers come from a per-(prefix,
/// year) sequence, the posting transaction simply executes its callback, and every persisted
/// work order is captured so tests can assert on workflow transitions without a database.
/// Mirrors <see cref="FakePurchaseRepository"/>.
/// </summary>
public sealed class FakeManufacturingRepository : IManufacturingRepository
{
    private readonly List<Workstation> _workstations = new();
    private readonly List<BillOfMaterials> _boms = new();
    private readonly List<WorkOrder> _workOrders = new();
    private readonly Dictionary<(string Prefix, int Year), int> _sequences = new();

    public IReadOnlyList<WorkOrder> WorkOrders => _workOrders;

    /// <summary>Number of transactions opened (proves the posting runs inside ONE transaction).</summary>
    public int TransactionCount { get; private set; }

    /// <summary>Pre-loads workstations (operation costing resolves their hourly rates).</summary>
    public void SeedWorkstation(params Workstation[] workstations) => _workstations.AddRange(workstations);

    /// <summary>Pre-loads BOMs with their items and operations.</summary>
    public void SeedBom(params BillOfMaterials[] boms) => _boms.AddRange(boms);

    /// <summary>Pre-loads work orders (workflow tests start from Draft/Submitted/InProcess directly).</summary>
    public void SeedWorkOrder(params WorkOrder[] workOrders) => _workOrders.AddRange(workOrders);

    public Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        TransactionCount++;
        return operation(cancellationToken);
    }

    public Task<string> NextWorkOrderNumberAsync(
        Guid companyId, string prefix, int year, CancellationToken cancellationToken = default)
    {
        var key = (prefix, year);
        _sequences.TryGetValue(key, out var current);
        _sequences[key] = current + 1;
        return Task.FromResult($"{prefix}-{year}-{current + 1:D5}");
    }

    public Task<Workstation?> GetWorkstationByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_workstations.FirstOrDefault(w => w.Id == id));

    public Task<BillOfMaterials?> GetBomByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_boms.FirstOrDefault(b => b.Id == id));

    public Task<BillOfMaterials?> GetDefaultActiveBomByItemIdAsync(Guid itemId, CancellationToken cancellationToken = default)
        => Task.FromResult(_boms.FirstOrDefault(b => b.ItemId == itemId && b.IsActive && b.IsDefault));

    // In-memory fakes ignore paging and return the whole seeded set (see FakeCustomerRepository).
    public Task<PagedResult<BillOfMaterials>> ListBomsAsync(Guid companyId, PagedRequest paging, CancellationToken cancellationToken = default)
    {
        var items = _boms.Where(b => b.CompanyId == companyId).OrderBy(b => b.BomNumber).ToList();
        return Task.FromResult(new PagedResult<BillOfMaterials>(items, items.Count, paging.SafePageNumber, paging.SafePageSize));
    }

    public Task<PagedResult<WorkOrder>> ListWorkOrdersAsync(Guid companyId, PagedRequest paging, CancellationToken cancellationToken = default)
    {
        var items = _workOrders.Where(o => o.CompanyId == companyId).OrderByDescending(o => o.CreatedAt).ToList();
        return Task.FromResult(new PagedResult<WorkOrder>(items, items.Count, paging.SafePageNumber, paging.SafePageSize));
    }

    public Task<WorkOrder?> GetWorkOrderByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_workOrders.FirstOrDefault(o => o.Id == id));

    public Task AddWorkstationAsync(Workstation workstation, CancellationToken cancellationToken = default)
    {
        _workstations.Add(workstation);
        return Task.CompletedTask;
    }

    public Task AddBomAsync(BillOfMaterials bom, CancellationToken cancellationToken = default)
    {
        _boms.Add(bom);
        return Task.CompletedTask;
    }

    public Task AddWorkOrderAsync(WorkOrder workOrder, CancellationToken cancellationToken = default)
    {
        _workOrders.Add(workOrder);
        return Task.CompletedTask;
    }

    /// <summary>
    /// When set, the NEXT <c>UpdateWorkOrderAsync</c> fails like the real repository does after a
    /// RowVersion mismatch (<c>DbUpdateConcurrencyException</c> translated to
    /// <see cref="ConcurrencyConflictException"/>); the flag resets itself so only one call fails.
    /// </summary>
    public bool FailNextWorkOrderUpdate { get; set; }

    public Task UpdateWorkOrderAsync(WorkOrder workOrder, CancellationToken cancellationToken = default)
    {
        if (FailNextWorkOrderUpdate)
        {
            FailNextWorkOrderUpdate = false;
            throw new ConcurrencyConflictException(nameof(WorkOrder), workOrder.Id);
        }

        // In-memory: the entity instance IS the store; workflow mutations are already applied.
        return Task.CompletedTask;
    }
}
