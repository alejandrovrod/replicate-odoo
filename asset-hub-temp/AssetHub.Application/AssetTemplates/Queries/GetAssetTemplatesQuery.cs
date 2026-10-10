using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.AssetTemplates;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.AssetTemplates.Queries;
public record GetAssetTemplatesQuery(string? SearchTerm = null) : IRequest<List<AssetTemplateDto>>;

public record AssetTemplateDto(Guid Id, Guid BusinessEntityTypeId, string Code, string Name, string Description, string SchemaJson, List<Guid> AllowedChildTemplateIds, LifecycleConfig LifecycleStates, string MaintenanceChecklist, int Version, bool IsActive, bool IsSystemTemplate);

public class GetAssetTemplatesQueryHandler : IRequestHandler<GetAssetTemplatesQuery, List<AssetTemplateDto>>
{
    private readonly ITenantDbContext _dbContext;

    private readonly ITenantResolver _tenantResolver;

    public GetAssetTemplatesQueryHandler(ITenantDbContext dbContext, ITenantResolver tenantResolver)
    {
        _dbContext = dbContext;
        _tenantResolver = tenantResolver;
    }

    public async Task<List<AssetTemplateDto>> Handle(GetAssetTemplatesQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId();
        
        var query = _dbContext.AssetTemplates
            .Where(t => (t.TenantId == tenantId || t.TenantId == null) && t.IsActive);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var search = request.SearchTerm.ToLower();
            query = query.Where(t => t.Name.ToLower().Contains(search) || t.Code.ToLower().Contains(search));
        }

        var templates = await query.ToListAsync(cancellationToken);

        return templates.Select(t => new AssetTemplateDto(
            t.Id,
            t.BusinessEntityTypeId,
            t.Code,
            t.Name,
            t.Description,
            t.SchemaJson,
            t.AllowedChildTemplateIds,
            t.LifecycleStates,
            t.MaintenanceChecklist,
            t.Version,
            t.IsActive,
            !t.TenantId.HasValue
        )).ToList();
    }
}
