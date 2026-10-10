using System;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Inventory.Commands;

public class CreateWarehouseCommand : IRequest<Guid>
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SuggestedLocation { get; set; }
}

public class CreateWarehouseCommandHandler : IRequestHandler<CreateWarehouseCommand, Guid>
{
    private readonly ITenantDbContext _dbContext;
    private readonly ITenantResolver _tenantResolver;

    public CreateWarehouseCommandHandler(ITenantDbContext dbContext, ITenantResolver tenantResolver)
    {
        _dbContext = dbContext;
        _tenantResolver = tenantResolver;
    }

    public async Task<Guid> Handle(CreateWarehouseCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("El nombre del almacén es requerido.");
        if (string.IsNullOrWhiteSpace(request.Code))
            throw new InvalidOperationException("El código del almacén es requerido.");

        var tenantId = _tenantResolver.GetCurrentTenantId().Value;

        // Check uniqueness
        var existing = await _dbContext.Warehouses
            .AnyAsync(w => w.TenantId == tenantId && w.Code == request.Code, cancellationToken);

        if (existing)
            throw new InvalidOperationException($"Ya existe un almacén con el código {request.Code}.");

        var warehouse = new Warehouse
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = request.Code,
            Name = request.Name,
            Description = request.Description,
            SuggestedLocation = request.SuggestedLocation,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Warehouses.Add(warehouse);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return warehouse.Id;
    }
}
