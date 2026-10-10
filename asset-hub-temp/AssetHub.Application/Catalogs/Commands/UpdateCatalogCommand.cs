using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Catalogs.Commands;

public record UpdateCatalogCommand(Guid Id, string Label, List<string>? TargetModules) : IRequest<bool>;

public class UpdateCatalogCommandHandler : IRequestHandler<UpdateCatalogCommand, bool>
{
    private readonly ITenantDbContext _dbContext;

    public UpdateCatalogCommandHandler(ITenantDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> Handle(UpdateCatalogCommand request, CancellationToken cancellationToken)
    {
        var catalog = await _dbContext.Catalogs.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (catalog == null)
            return false;

        catalog.Label = request.Label;
        catalog.TargetModulesJson = request.TargetModules != null ? JsonSerializer.Serialize(request.TargetModules) : null;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
