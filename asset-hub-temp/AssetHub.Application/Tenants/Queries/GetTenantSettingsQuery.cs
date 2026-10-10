using System;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Tenants.Queries;

public record TenantSettingsDto(string? LogoUrl, string? SupportEmail);

public record GetTenantSettingsQuery : IRequest<TenantSettingsDto>;

public class GetTenantSettingsQueryHandler : IRequestHandler<GetTenantSettingsQuery, TenantSettingsDto>
{
    private readonly IPlatformDbContext _platformDbContext;
    private readonly ITenantResolver _tenantResolver;

    public GetTenantSettingsQueryHandler(IPlatformDbContext platformDbContext, ITenantResolver tenantResolver)
    {
        _platformDbContext = platformDbContext;
        _tenantResolver = tenantResolver;
    }

    public async Task<TenantSettingsDto> Handle(GetTenantSettingsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId()
            ?? throw new UnauthorizedAccessException("Tenant is required.");

        var tenant = await _platformDbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

        if (tenant == null)
        {
            throw new InvalidOperationException("Tenant not found.");
        }

        return new TenantSettingsDto(tenant.LogoUrl, tenant.SupportEmail);
    }
}
