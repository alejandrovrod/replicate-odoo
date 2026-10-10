using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Catalogs.Queries;

public record GetCatalogsQuery() : IRequest<List<CatalogDto>>;

public record CatalogDto(Guid Id, string Code, string Label, bool IsGlobal, List<string> TargetModules);

public class GetCatalogsQueryHandler : IRequestHandler<GetCatalogsQuery, List<CatalogDto>>
{
    private readonly ITenantDbContext _dbContext;

    public GetCatalogsQueryHandler(ITenantDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<CatalogDto>> Handle(GetCatalogsQuery request, CancellationToken cancellationToken)
    {
        // Trae los catálogos del tenant y los globales.
        // El filtro global de EntityFramework se encarga de esto.
        var catalogs = await _dbContext.Catalogs.ToListAsync(cancellationToken);

        return catalogs.Select(c => new CatalogDto(
            c.Id, 
            c.Code, 
            c.Label, 
            c.TenantId == null,
            string.IsNullOrEmpty(c.TargetModulesJson) 
                ? new List<string>() 
                : System.Text.Json.JsonSerializer.Deserialize<List<string>>(c.TargetModulesJson) ?? new List<string>()
        )).ToList();
    }
}
