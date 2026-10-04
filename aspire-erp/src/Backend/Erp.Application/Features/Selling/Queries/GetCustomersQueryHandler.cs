using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Selling.Queries;

/// <summary>Assembles <see cref="GetCustomersQuery"/> from <see cref="ICustomerRepository"/>.</summary>
public sealed class GetCustomersQueryHandler : IQueryHandler<GetCustomersQuery, IReadOnlyList<CustomerDto>>
{
    private readonly ICustomerRepository _customers;

    public GetCustomersQueryHandler(ICustomerRepository customers)
    {
        _customers = customers;
    }

    public async Task<IReadOnlyList<CustomerDto>> HandleAsync(
        GetCustomersQuery query,
        CancellationToken cancellationToken = default)
    {
        var limit = query.Limit <= 0 ? 50 : Math.Min(query.Limit, 500);
        var customers = await _customers.GetRecentAsync(query.CompanyId, limit, cancellationToken);
        return customers.Select(CustomerDto.From).ToList();
    }
}
