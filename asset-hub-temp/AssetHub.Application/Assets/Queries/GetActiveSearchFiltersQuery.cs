using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Assets.Queries;

public record GetActiveSearchFiltersQuery() : IRequest<List<SearchFilterDto>>;

public record SearchFilterDto(string AttributeKey, string AttributeLabel, List<CatalogItemFilterDto> Options);

public record CatalogItemFilterDto(Guid CatalogItemId, string Code, string Label);

public class GetActiveSearchFiltersQueryHandler : IRequestHandler<GetActiveSearchFiltersQuery, List<SearchFilterDto>>
{
    private readonly ITenantDbContext _dbContext;

    public GetActiveSearchFiltersQueryHandler(ITenantDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<SearchFilterDto>> Handle(GetActiveSearchFiltersQuery request, CancellationToken cancellationToken)
    {
        // Get all catalogs that are configured for the 'Assets' module
        var catalogsForAssets = await _dbContext.Catalogs
            .Where(c => c.TargetModulesJson != null && c.TargetModulesJson.Contains("\"Assets\""))
            .ToListAsync(cancellationToken);

        if (!catalogsForAssets.Any())
            return new List<SearchFilterDto>();

        var catalogIds = catalogsForAssets.Select(c => c.Id).ToList();
        
        var catalogItems = await _dbContext.CatalogItems
            .Include(c => c.Translations)
            .Where(c => catalogIds.Contains(c.CatalogId))
            .ToListAsync(cancellationToken);

        var result = catalogsForAssets
            .Select(c => new SearchFilterDto(
                c.Code, // Use catalog code as AttributeKey since we aren't using EAV attributes to group anymore
                c.Label,
                catalogItems
                 .Where(ci => ci.CatalogId == c.Id)
                 .Select(item => 
                 {
                     var label = item.Translations.FirstOrDefault(t => t.Locale == "es")?.Label ?? item.Code;
                     return new CatalogItemFilterDto(item.Id, item.Code, label);
                 })
                 .ToList()
            ))
            .ToList();

        return result;
    }
}
