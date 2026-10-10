using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Exceptions;
using AssetHub.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Inventory.Commands;

public class UpdateInventorySettingsCommand : IRequest<InventorySettingsDto>
{
    public string OperatingMode { get; set; } = string.Empty;
    public bool AllowNegativeStock { get; set; }
}

public class InventorySettingsDto
{
    public Guid TenantId { get; set; }
    public string OperatingMode { get; set; } = InventoryOperatingMode.External;
    public bool AllowNegativeStock { get; set; }
}

public class UpdateInventorySettingsCommandHandler : IRequestHandler<UpdateInventorySettingsCommand, InventorySettingsDto>
{
    private readonly ITenantDbContext _dbContext;
    private readonly ITenantResolver _tenantResolver;

    public UpdateInventorySettingsCommandHandler(ITenantDbContext dbContext, ITenantResolver tenantResolver)
    {
        _dbContext = dbContext;
        _tenantResolver = tenantResolver;
    }

    public async Task<InventorySettingsDto> Handle(UpdateInventorySettingsCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId();

        if (!tenantId.HasValue)
        {
            throw new InvalidOperationException("No se pudo resolver el tenant.");
        }

        var mode = request.OperatingMode?.Trim().ToLowerInvariant();

        if (mode != InventoryOperatingMode.External &&
            mode != InventoryOperatingMode.Internal &&
            mode != InventoryOperatingMode.Hybrid)
        {
            throw new DomainException(
                "invalid_inventory_mode",
                $"El modo de operación '{request.OperatingMode}' no es válido. Valores permitidos: external, internal, hybrid.",
                "Domain.InvalidInventoryMode",
                request.OperatingMode);
        }

        var settings = await _dbContext.TenantInventorySettings
            .FirstOrDefaultAsync(s => s.TenantId == tenantId.Value, cancellationToken);

        var now = DateTime.UtcNow;

        if (settings == null)
        {
            settings = new TenantInventorySettings
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId.Value,
                Enabled = true,
                CreatedAt = now
            };
            _dbContext.TenantInventorySettings.Add(settings);
        }

        settings.OperatingMode = mode;
        settings.AllowNegativeStock = request.AllowNegativeStock;
        settings.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new InventorySettingsDto
        {
            TenantId = settings.TenantId,
            OperatingMode = settings.OperatingMode,
            AllowNegativeStock = settings.AllowNegativeStock
        };
    }
}
