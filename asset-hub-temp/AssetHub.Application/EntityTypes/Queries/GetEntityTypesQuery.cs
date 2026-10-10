using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.EntityTypes.Queries;

public record GetEntityTypesQuery() : IRequest<List<EntityTypeDto>>;

public record EntityTypeDto(Guid Id, string Code, string Name, string Description, string Icon, List<string> EnabledModules, List<Guid> DefaultCatalogIds, bool IsSystemCategory);

public class GetEntityTypesQueryHandler : IRequestHandler<GetEntityTypesQuery, List<EntityTypeDto>>
{
    private readonly ITenantDbContext _dbContext;

    public GetEntityTypesQueryHandler(ITenantDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<EntityTypeDto>> Handle(GetEntityTypesQuery request, CancellationToken cancellationToken)
    {
        var types = await _dbContext.BusinessEntityTypes
            .Where(t => t.IsActive)
            .ToListAsync(cancellationToken);

        return types.Select(t => new EntityTypeDto(
            t.Id,
            t.Code,
            t.Name,
            t.Description,
            t.Icon,
            t.EnabledModules,
            t.DefaultCatalogIds,
            !t.TenantId.HasValue
        )).ToList();
    }
}
