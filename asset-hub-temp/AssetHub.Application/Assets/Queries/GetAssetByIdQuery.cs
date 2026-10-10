using System;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using AssetHub.Domain.AssetTemplates;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Collections.Generic;
using NetTopologySuite.IO;

namespace AssetHub.Application.Assets.Queries;

public record GetAssetByIdQuery(Guid Id) : IRequest<AssetDetailDto?>;

public record AssetDetailDto(
    Guid Id, 
    Guid TemplateId, 
    string TemplateName,
    string SchemaJson,
    LifecycleConfig LifecycleStates,
    Guid? ParentId, 
    string Path, 
    string Code, 
    string Name, 
    string State, 
    decimal? ConditionIndex,
    string PropertiesJson,
    DateTime? InstalledAt,
    DateTime? CommissionedAt,
    double? Latitude,
    double? Longitude,
    string? GeoJson
);

public class GetAssetByIdQueryHandler : IRequestHandler<GetAssetByIdQuery, AssetDetailDto?>
{
    private readonly ITenantDbContext _dbContext;

    public GetAssetByIdQueryHandler(ITenantDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AssetDetailDto?> Handle(GetAssetByIdQuery request, CancellationToken cancellationToken)
    {
        var asset = await _dbContext.Assets
            .Include(a => a.AssetTemplate)
            .FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken);

        if (asset == null || asset.AssetTemplate == null) return null;

        var propertiesJson = asset.PropertiesJson;
        if (!string.IsNullOrEmpty(propertiesJson) && !string.IsNullOrEmpty(asset.AssetTemplate.SchemaJson))
        {
            try
            {
                var schemaObj = JsonNode.Parse(asset.AssetTemplate.SchemaJson);
                var schemaProperties = schemaObj?["properties"]?.AsObject();
                if (schemaProperties != null)
                {
                    var propsObj = JsonNode.Parse(propertiesJson)?.AsObject();
                    if (propsObj != null)
                    {
                        var keysToRemove = new List<string>();
                        foreach (var kvp in propsObj)
                        {
                            if (!schemaProperties.ContainsKey(kvp.Key))
                            {
                                keysToRemove.Add(kvp.Key);
                            }
                        }
                        foreach (var key in keysToRemove)
                        {
                            propsObj.Remove(key);
                        }
                        propertiesJson = propsObj.ToJsonString();
                    }
                }
            }
            catch { /* Ignore parsing errors, return original */ }
        }

        string? geoJsonStr = null;
        if (asset.Geo != null)
        {
            var writer = new GeoJsonWriter();
            geoJsonStr = writer.Write(asset.Geo);
        }

        return new AssetDetailDto(
            asset.Id,
            asset.AssetTemplateId,
            asset.AssetTemplate.Name,
            asset.AssetTemplate.SchemaJson,
            asset.AssetTemplate.LifecycleStates,
            asset.ParentId,
            asset.Path,
            asset.Code,
            asset.Name,
            asset.State,
            asset.ConditionIndex,
            propertiesJson,
            asset.InstalledAt,
            asset.CommissionedAt,
            asset.Geo?.Coordinate?.Y,
            asset.Geo?.Coordinate?.X,
            geoJsonStr
        );
    }
}
