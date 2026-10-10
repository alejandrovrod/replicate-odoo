using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.IO;

namespace AssetHub.Application.Assets.Queries;

public record SearchAssetsQuery(string? SearchTerm, Guid? TemplateId, string? State, Dictionary<string, Guid>? CatalogFilters = null, Guid? AncestorId = null, bool? RootOnly = false, int Page = 1, int PageSize = 50) : IRequest<PagedResult<AssetDto>>;

public record AssetSummaryDto(Guid Id, string Code, string Name, string State, string? StateColor);

public record AssetDto(
    Guid Id, 
    Guid TemplateId, 
    Guid? ParentId, 
    string Path, 
    string PathNames, 
    string Code, 
    string Name, 
    string State, 
    string? StateColor, 
    decimal? ConditionIndex, 
    int ChildrenCount, 
    double? Latitude, 
    double? Longitude, 
    string? GeoJson,
    string? HealthRiskLevel = null,
    decimal? HealthRiskProbability = null,
    int? HealthPredictedFailureDays = null
);

public class SearchAssetsQueryHandler : IRequestHandler<SearchAssetsQuery, PagedResult<AssetDto>>
{
    private readonly ITenantDbContext _dbContext;

    public SearchAssetsQueryHandler(ITenantDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<AssetDto>> Handle(SearchAssetsQuery request, CancellationToken cancellationToken)
    {
        var q = _dbContext.Assets
            .Include(a => a.AssetTemplate)
            .Where(a => !a.IsDeleted)
            .AsQueryable();

        if (request.RootOnly == true)
        {
            q = q.Where(a => a.ParentId == null);
        }

        if (!string.IsNullOrEmpty(request.SearchTerm))
        {
            q = q.Where(a => a.Code.Contains(request.SearchTerm) || a.Name.Contains(request.SearchTerm));
        }

        if (request.TemplateId.HasValue)
        {
            q = q.Where(a => a.AssetTemplateId == request.TemplateId);
        }

        if (!string.IsNullOrEmpty(request.State))
        {
            q = q.Where(a => a.State == request.State);
        }

        if (request.AncestorId.HasValue)
        {
            var ancestorId = request.AncestorId.Value;
            var descendantIds = _dbContext.AssetHierarchies
                .Where(h => h.AncestorId == ancestorId && h.Depth > 0)
                .Select(h => h.DescendantId);
            
            q = q.Where(a => descendantIds.Contains(a.Id));
        }

        if (request.CatalogFilters != null && request.CatalogFilters.Any())
        {
            foreach (var filter in request.CatalogFilters)
            {
                var attributeKey = filter.Key;
                var catalogItemId = filter.Value;
                var catalogItemIdStr = filter.Value.ToString();

                // Buscar en el viejo EAV
                var assetIdsWithAttr = _dbContext.AssetAttributeValues
                    .Where(v => v.AttributeKey == attributeKey && v.ValueCatalogItemId == catalogItemId)
                    .Select(v => v.AssetId);

                // Y buscar en el nuevo PropertiesJson
                q = q.Where(a => (a.PropertiesJson != null && a.PropertiesJson.Contains(catalogItemIdStr)) || assetIdsWithAttr.Contains(a.Id));
            }
        }

        var totalCount = await q.CountAsync(cancellationToken);
        
        q = q.OrderBy(a => a.Code) // Añadir ordenamiento por defecto para Skip/Take consistente
             .Skip((request.Page - 1) * request.PageSize)
             .Take(request.PageSize);

        var assets = await q.ToListAsync(cancellationToken);
        
        // Fetch child counts for the matched assets
        var matchedIds = assets.Select(a => a.Id).ToList();
        var childrenCounts = new Dictionary<Guid, int>();
        if (matchedIds.Any())
        {
            childrenCounts = await _dbContext.Assets
                .Where(a => !a.IsDeleted && a.ParentId.HasValue && matchedIds.Contains(a.ParentId.Value))
                .GroupBy(a => a.ParentId!.Value)
                .Select(g => new { ParentId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ParentId, x => x.Count, cancellationToken);
        }

        // Extraer todos los IDs de las rutas para buscar sus nombres
        var pathIds = new HashSet<Guid>();
        foreach(var a in assets) 
        {
            if (string.IsNullOrEmpty(a.Path)) continue;
            var parts = a.Path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            foreach(var p in parts) 
            {
                if (Guid.TryParse(p, out var g)) pathIds.Add(g);
            }
        }

        var assetNames = await _dbContext.Assets
            .Where(a => pathIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.Name, cancellationToken);

        // Fetch predictions for the matched assets
        var predictions = new Dictionary<Guid, (string RiskLevel, decimal RiskProbability, int? PredictedFailureDays)>();
        if (matchedIds.Any())
        {
            var rawPreds = await _dbContext.AssetHealthPredictions
                .Where(p => matchedIds.Contains(p.AssetId))
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new { p.AssetId, p.RiskLevel, p.RiskProbability, p.PredictedFailureDays })
                .ToListAsync(cancellationToken);

            foreach (var p in rawPreds)
            {
                if (!predictions.ContainsKey(p.AssetId))
                {
                    predictions[p.AssetId] = (p.RiskLevel, p.RiskProbability, p.PredictedFailureDays);
                }
            }
        }

        var geoJsonWriter = new GeoJsonWriter();

        var dtos = assets.Select(a => {
            var pathNames = "/";
            if (!string.IsNullOrEmpty(a.Path)) 
            {
                var parts = a.Path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                var names = parts.Select(p => Guid.TryParse(p, out var g) && assetNames.TryGetValue(g, out var name) ? name : p);
                pathNames = "/" + string.Join("/", names) + "/";
            }

            var childrenCount = childrenCounts.TryGetValue(a.Id, out var c) ? c : 0;

            string? aColor = null;
            if (a.AssetTemplate?.LifecycleStates?.States != null && a.AssetTemplate.LifecycleStates.States.TryGetValue(a.State, out var aStateConfig))
            {
                aColor = aStateConfig.Color;
            }
            
            string? geoJsonStr = null;
            if (a.Geo != null)
            {
                geoJsonStr = geoJsonWriter.Write(a.Geo);
            }

            string? predRiskLevel = null;
            decimal? predRiskProbability = null;
            int? predPredictedFailureDays = null;

            if (predictions.TryGetValue(a.Id, out var pred))
            {
                predRiskLevel = pred.RiskLevel;
                predRiskProbability = pred.RiskProbability;
                predPredictedFailureDays = pred.PredictedFailureDays;
            }

            return new AssetDto(
                a.Id,
                a.AssetTemplateId,
                a.ParentId,
                a.Path,
                pathNames,
                a.Code,
                a.Name,
                a.State,
                aColor,
                a.ConditionIndex,
                childrenCount,
                a.Geo?.Coordinate?.Y, // Y is Latitude in standard GIS, or X depending on how it was stored. Usually Y=Lat, X=Lng.
                a.Geo?.Coordinate?.X,
                geoJsonStr,
                predRiskLevel,
                predRiskProbability,
                predPredictedFailureDays
            );
        }).ToList();

        return new PagedResult<AssetDto>(dtos, totalCount, request.Page, request.PageSize);
    }
}
