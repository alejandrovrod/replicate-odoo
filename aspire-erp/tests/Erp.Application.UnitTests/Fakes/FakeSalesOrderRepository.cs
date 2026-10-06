using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.UnitTests.Fakes;

/// <summary>
/// In-memory <see cref="ISalesOrderRepository"/>: gapless SO numbers come from a per-(company, year)
/// sequence, the creation transaction simply executes its callback, and every persisted order is
/// captured so tests can assert on SalesOrder rows without a database.
/// </summary>
public sealed class FakeSalesOrderRepository : ISalesOrderRepository
{
    private readonly List<SalesOrder> _orders = new();
    private readonly Dictionary<(Guid CompanyId, int Year), int> _sequences = new();

    public IReadOnlyList<SalesOrder> Orders => _orders;

    /// <summary>Number of transactions opened (proves the creation runs inside ONE transaction).</summary>
    public int TransactionCount { get; private set; }

    /// <summary>
    /// When set, the NEXT <c>UpdateOrderAsync</c> fails like the real repository does after a
    /// RowVersion mismatch (<c>DbUpdateConcurrencyException</c> translated to
    /// <see cref="ConcurrencyConflictException"/>); the flag resets itself so only one call fails.
    /// </summary>
    public bool FailNextOrderUpdate { get; set; }

    /// <summary>Pre-loads an order (workflow/posting tests can start from any status directly).</summary>
    public void SeedOrder(params SalesOrder[] orders) => _orders.AddRange(orders);

    public Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        TransactionCount++;
        return operation(cancellationToken);
    }

    public Task<string> NextOrderNumberAsync(
        Guid companyId, int year, CancellationToken cancellationToken = default)
    {
        var key = (companyId, year);
        _sequences.TryGetValue(key, out var current);
        _sequences[key] = current + 1;
        return Task.FromResult($"SO-{year}-{current + 1:D5}");
    }

    public Task AddOrderAsync(SalesOrder order, CancellationToken cancellationToken = default)
    {
        _orders.Add(order);
        return Task.CompletedTask;
    }

    public Task UpdateOrderAsync(SalesOrder order, CancellationToken cancellationToken = default)
    {
        if (FailNextOrderUpdate)
        {
            FailNextOrderUpdate = false;
            throw new ConcurrencyConflictException(nameof(SalesOrder), order.Id);
        }

        // In-memory: the entity instance IS the store; workflow mutations are already applied.
        return Task.CompletedTask;
    }

    public Task<SalesOrder?> GetOrderByIdAsync(
        Guid salesOrderId, CancellationToken cancellationToken = default)
        => Task.FromResult(_orders.FirstOrDefault(o => o.Id == salesOrderId));

    // In-memory fakes ignore paging and return the whole seeded set (see FakeCustomerRepository).
    public Task<PagedResult<SalesOrder>> GetRecentOrdersByCompanyAsync(
        Guid companyId, PagedRequest paging, CancellationToken cancellationToken = default)
    {
        var items = _orders.Where(o => o.CompanyId == companyId).ToList();
        return Task.FromResult(new PagedResult<SalesOrder>(items, items.Count, paging.SafePageNumber, paging.SafePageSize));
    }
}
