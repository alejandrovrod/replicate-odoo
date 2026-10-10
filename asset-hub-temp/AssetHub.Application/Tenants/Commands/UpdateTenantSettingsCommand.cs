using System;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Tenants.Commands;

public record UpdateTenantSettingsCommand(string? LogoUrl, string? SupportEmail) : IRequest<bool>;

public class UpdateTenantSettingsCommandHandler : IRequestHandler<UpdateTenantSettingsCommand, bool>
{
    private readonly IPlatformDbContext _platformDbContext;
    private readonly ITenantResolver _tenantResolver;

    public UpdateTenantSettingsCommandHandler(IPlatformDbContext platformDbContext, ITenantResolver tenantResolver)
    {
        _platformDbContext = platformDbContext;
        _tenantResolver = tenantResolver;
    }

    public async Task<bool> Handle(UpdateTenantSettingsCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId()
            ?? throw new UnauthorizedAccessException("Tenant is required.");

        var tenant = await _platformDbContext.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

        if (tenant == null)
        {
            throw new InvalidOperationException("Tenant not found.");
        }

        tenant.LogoUrl = request.LogoUrl;
        tenant.SupportEmail = request.SupportEmail;

        await _platformDbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
