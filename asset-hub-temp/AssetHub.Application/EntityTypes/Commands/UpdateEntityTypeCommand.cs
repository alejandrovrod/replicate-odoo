using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.EntityTypes.Commands;

public record UpdateEntityTypeCommand(string Code, string Name, string Description, string Icon, List<string> EnabledModules, List<Guid> DefaultCatalogIds) : IRequest<bool>;

public class UpdateEntityTypeCommandHandler : IRequestHandler<UpdateEntityTypeCommand, bool>
{
    private readonly ITenantDbContext _dbContext;

    public UpdateEntityTypeCommandHandler(ITenantDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> Handle(UpdateEntityTypeCommand request, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.BusinessEntityTypes
            .FirstOrDefaultAsync(e => e.Code == request.Code && e.IsActive, cancellationToken);

        if (entity == null) return false;

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

        entity.Name = request.Name;
        entity.Description = request.Description;
        entity.Icon = request.Icon;
        entity.EnabledModules = request.EnabledModules ?? new List<string>();
        entity.DefaultCatalogIds = request.DefaultCatalogIds ?? new List<Guid>();

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
