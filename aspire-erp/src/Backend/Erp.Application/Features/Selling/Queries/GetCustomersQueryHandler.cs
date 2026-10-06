using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Selling.Queries;

/// <summary>Assembles <see cref="GetCustomersQuery"/> from <see cref="ICustomerRepository"/>.</summary>
public sealed class GetCustomersQueryHandler : IQueryHandler<GetCustomersQuery, PagedResult<CustomerDto>>
{
    private readonly ICustomerRepository _customers;

    public GetCustomersQueryHandler(ICustomerRepository customers)
    {
        _customers = customers;
    }

    public async Task<PagedResult<CustomerDto>> HandleAsync(
        GetCustomersQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = await _customers.GetRecentAsync(
            query.CompanyId,
            new PagedRequest(query.PageNumber, query.PageSize),
            cancellationToken);
        return page.Map(page.Items.Select(CustomerDto.From).ToList());
    }
}
