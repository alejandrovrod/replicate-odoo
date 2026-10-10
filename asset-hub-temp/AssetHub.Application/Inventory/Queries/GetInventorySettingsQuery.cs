using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Inventory.Queries;

public class TenantInventorySettingsDto
{
    public Guid TenantId { get; set; }
    public string OperatingMode { get; set; } = InventoryOperatingMode.External;
    public bool AllowNegativeStock { get; set; }
}

public class GetInventorySettingsQuery : IRequest<TenantInventorySettingsDto>
{
}

public class GetInventorySettingsQueryHandler : IRequestHandler<GetInventorySettingsQuery, TenantInventorySettingsDto>
{
    private readonly ITenantDbContext _dbContext;
    private readonly ITenantResolver _tenantResolver;

    public GetInventorySettingsQueryHandler(ITenantDbContext dbContext, ITenantResolver tenantResolver)
    {
        _dbContext = dbContext;
        _tenantResolver = tenantResolver;
    }

    public async Task<TenantInventorySettingsDto> Handle(GetInventorySettingsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId();

        if (!tenantId.HasValue)
        {
            throw new InvalidOperationException("No se pudo resolver el tenant.");
        }

        var settings = await _dbContext.TenantInventorySettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId.Value, cancellationToken);

        if (settings == null)
        {
            // No settings row yet: report defaults without creating one.
            return new TenantInventorySettingsDto
            {
                TenantId = tenantId.Value,
                OperatingMode = InventoryOperatingMode.External,
                AllowNegativeStock = false
            };
        }

        return new TenantInventorySettingsDto
        {
            TenantId = settings.TenantId,
            OperatingMode = settings.OperatingMode,
            AllowNegativeStock = settings.AllowNegativeStock
        };
    }
}
