using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Selling.Queries;

/// <summary>Single-customer read of <see cref="GetCustomerByIdQuery"/> (null = 404 at the API).</summary>
public sealed class GetCustomerByIdQueryHandler : IQueryHandler<GetCustomerByIdQuery, CustomerDto?>
{
    private readonly ICustomerRepository _customers;

    public GetCustomerByIdQueryHandler(ICustomerRepository customers)
    {
        _customers = customers;
    }

    public async Task<CustomerDto?> HandleAsync(
        GetCustomerByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var customer = await _customers.GetByIdAsync(query.CustomerId, cancellationToken);
        if (customer is null || customer.CompanyId != query.CompanyId)
        {
            return null;
        }

        return CustomerDto.From(customer);
    }
}
