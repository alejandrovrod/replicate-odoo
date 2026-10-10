using System;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Catalogs;
using MediatR;
using Microsoft.EntityFrameworkCore;

using System.Text.Json;

namespace AssetHub.Application.Catalogs.Commands;

public record CreateCatalogCommand(string Code, string Label, List<string>? TargetModules = null, bool IsSystem = false) : IRequest<Guid>;

public class CreateCatalogCommandHandler : IRequestHandler<CreateCatalogCommand, Guid>
{
    private readonly ITenantDbContext _dbContext;
    private readonly ITenantResolver _tenantResolver;

    public CreateCatalogCommandHandler(ITenantDbContext dbContext, ITenantResolver tenantResolver)
    {
        _dbContext = dbContext;
        _tenantResolver = tenantResolver;
    }

    public async Task<Guid> Handle(CreateCatalogCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId();

        var exists = await _dbContext.Catalogs.AnyAsync(c => c.Code == request.Code && c.TenantId == tenantId, cancellationToken);
        if (exists)
            throw new InvalidOperationException($"Ya existe un catálogo con código {request.Code}");

        var catalog = new Catalog
        {
            TenantId = tenantId,
            Code = request.Code,
            Label = request.Label,
            TargetModulesJson = request.TargetModules != null ? JsonSerializer.Serialize(request.TargetModules) : null,
            IsSystem = request.IsSystem
        };

        _dbContext.Catalogs.Add(catalog);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return catalog.Id;
    }
}
