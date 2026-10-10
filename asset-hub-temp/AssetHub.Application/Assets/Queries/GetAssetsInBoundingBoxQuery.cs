using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;

namespace AssetHub.Application.Assets.Queries;

public record GetAssetsInBoundingBoxQuery(double MinLon, double MinLat, double MaxLon, double MaxLat) : IRequest<FeatureCollection>;

public class GetAssetsInBoundingBoxQueryHandler : IRequestHandler<GetAssetsInBoundingBoxQuery, FeatureCollection>
{
    private readonly ITenantDbContext _dbContext;

    public GetAssetsInBoundingBoxQueryHandler(ITenantDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<FeatureCollection> Handle(GetAssetsInBoundingBoxQuery request, CancellationToken cancellationToken)
    {
        // Validar límite de área aprox (para simplificar, limitamos la diferencia en grados)
        if (Math.Abs(request.MaxLon - request.MinLon) > 5 || Math.Abs(request.MaxLat - request.MinLat) > 5)
        {
            throw new ArgumentException("Bounding box exceeds maximum allowed area");
        }

        var geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        var bbox = geometryFactory.CreatePolygon(new Coordinate[]
        {
            new Coordinate(request.MinLon, request.MinLat),
            new Coordinate(request.MaxLon, request.MinLat),
            new Coordinate(request.MaxLon, request.MaxLat),
            new Coordinate(request.MinLon, request.MaxLat),
            new Coordinate(request.MinLon, request.MinLat)
        });

        var assets = await _dbContext.Assets
            .Where(a => a.Geo != null && a.Geo.Intersects(bbox))
            .ToListAsync(cancellationToken);

        var featureCollection = new FeatureCollection();
        foreach (var a in assets)
        {
            var attributes = new AttributesTable
            {
                { "id", a.Id },
                { "name", a.Name },
                { "code", a.Code },
                { "state", a.State },
                { "templateId", a.AssetTemplateId }
            };
            var feature = new Feature(a.Geo, attributes);
            featureCollection.Add(feature);
        }

        return featureCollection;
    }
}
