using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.UnitTests.Fakes;

/// <summary>In-memory <see cref="ICustomerRepository"/> over a seeded customer set (Task 5.1).</summary>
public sealed class FakeCustomerRepository : ICustomerRepository
{
    private readonly List<Customer> _customers = new();

    /// <summary>Customers visible to the service under test.</summary>
    public IReadOnlyList<Customer> Customers => _customers;

    /// <summary>Last customer passed to AddAsync (null when nothing was created).</summary>
    public Customer? AddedCustomer { get; private set; }

    public void Seed(params Customer[] customers) => _customers.AddRange(customers);

    public Task AddAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        AddedCustomer = customer;
        _customers.Add(customer);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        // In-memory: the instance is already updated
        return Task.CompletedTask;
    }

    public Task<bool> ExistsCodeAsync(Guid companyId, string customerCode, CancellationToken cancellationToken = default)
        => Task.FromResult(_customers.Any(c => c.CompanyId == companyId && c.CustomerCode == customerCode));

    public Task<Customer?> GetByIdAsync(Guid customerId, CancellationToken cancellationToken = default)
        => Task.FromResult(_customers.FirstOrDefault(c => c.Id == customerId));

    // In-memory fakes ignore paging and return the whole seeded set: paging itself is
    // covered by the shared extension plus integration tests, handler tests assert mapping.
    public Task<PagedResult<Customer>> GetRecentAsync(
        Guid companyId,
        PagedRequest paging,
        CancellationToken cancellationToken = default)
        => Task.FromResult(
            new PagedResult<Customer>(
                _customers
                    .Where(c => c.CompanyId == companyId)
                    .OrderByDescending(c => c.Id)
                    .ToList(),
                _customers.Count(c => c.CompanyId == companyId),
                paging.SafePageNumber,
                paging.SafePageSize));
}
