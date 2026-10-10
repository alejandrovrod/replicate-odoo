using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Tenancy;
using MediatR;

namespace AssetHub.Application.Tenancy.Queries;

public record GetCurrentTenantQuery() : IRequest<Tenant?>;

public class GetCurrentTenantQueryHandler : IRequestHandler<GetCurrentTenantQuery, Tenant?>
{
    private readonly ITenantResolver _tenantResolver;

    public GetCurrentTenantQueryHandler(ITenantResolver tenantResolver)
    {
        _tenantResolver = tenantResolver;
    }

    public Task<Tenant?> Handle(GetCurrentTenantQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(_tenantResolver.GetCurrentTenant());
    }
}
