using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Catalogs.Queries;

public record GetCatalogItemsQuery(string CatalogCode, string Locale = "es", string? Search = null) : IRequest<List<CatalogItemDto>>;

public record CatalogItemDto(Guid Id, string Code, string Label, int Order, Guid? ParentItemId, bool IsOverride);

public class GetCatalogItemsQueryHandler : IRequestHandler<GetCatalogItemsQuery, List<CatalogItemDto>>
{
    private readonly ITenantDbContext _dbContext;

    public GetCatalogItemsQueryHandler(ITenantDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<CatalogItemDto>> Handle(GetCatalogItemsQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.CatalogItems
            .Include(ci => ci.Translations)
            .Where(ci => ci.Catalog!.Code == request.CatalogCode);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLowerInvariant();
            query = query.Where(ci => ci.Translations.Any(t =>
                t.Locale == request.Locale && t.Label.ToLower().Contains(term)));
        }

        var items = await query.ToListAsync(cancellationToken);

        // Agrupar por Code para procesar overrides.
        // Si hay un item con TenantId != null, pisa al que tiene TenantId == null
        var grouped = items.GroupBy(i => i.Code);
        
        var result = new List<CatalogItemDto>();

        foreach (var group in grouped)
        {
            var bestMatch = group.OrderByDescending(i => i.TenantId.HasValue ? 1 : 0).First();
            
            var translation = bestMatch.Translations.FirstOrDefault(t => t.Locale == request.Locale) 
                           ?? bestMatch.Translations.FirstOrDefault(t => t.Locale == "es");

            result.Add(new CatalogItemDto(
                bestMatch.Id,
                bestMatch.Code,
                translation?.Label ?? bestMatch.Code,
                bestMatch.Order,
                bestMatch.ParentItemId,
                bestMatch.TenantId.HasValue
            ));
        }

        return result.OrderBy(r => r.Order).ToList();
    }
}
