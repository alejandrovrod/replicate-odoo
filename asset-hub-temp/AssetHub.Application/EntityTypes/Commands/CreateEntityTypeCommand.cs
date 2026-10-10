using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.EntityTypes;
using AssetHub.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.EntityTypes.Commands;

public record CreateEntityTypeCommand(string Code, string Name, string Description, string Icon, List<string> EnabledModules, List<Guid> DefaultCatalogIds) : IRequest<Guid>;

public class CreateEntityTypeCommandHandler : IRequestHandler<CreateEntityTypeCommand, Guid>
{
    private readonly ITenantDbContext _dbContext;
    private readonly ITenantResolver _tenantResolver;

    public CreateEntityTypeCommandHandler(ITenantDbContext dbContext, ITenantResolver tenantResolver)
    {
        _dbContext = dbContext;
        _tenantResolver = tenantResolver;
    }

    public async Task<Guid> Handle(CreateEntityTypeCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId();

        var exists = await _dbContext.BusinessEntityTypes.AnyAsync(e => e.Code == request.Code && e.TenantId == tenantId && e.IsActive, cancellationToken);
        if (exists)
            throw new InvalidOperationException($"Ya existe un tipo de entidad activo con el código {request.Code}");

        // Validar catálogos
        if (request.DefaultCatalogIds != null && request.DefaultCatalogIds.Any())
        {
            var validCatalogs = await _dbContext.Catalogs
                .Where(c => request.DefaultCatalogIds.Contains(c.Id))
                .Select(c => c.Id)
                .ToListAsync(cancellationToken);

            if (validCatalogs.Count != request.DefaultCatalogIds.Count)
            {
                throw new InvalidCatalogException();
            }
        }

        // TODO: Validar que los EnabledModules estén permitidos por el plan del Tenant

        var entity = new BusinessEntityType
        {
            TenantId = tenantId.Value,
            Code = request.Code,
            Name = request.Name,
            Description = request.Description,
            Icon = request.Icon,
            EnabledModules = request.EnabledModules ?? new List<string>(),
            DefaultCatalogIds = request.DefaultCatalogIds ?? new List<Guid>(),
            IsActive = true
        };

        _dbContext.BusinessEntityTypes.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}
