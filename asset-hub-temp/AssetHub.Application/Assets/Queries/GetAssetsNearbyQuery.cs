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

public record GetAssetsNearbyQuery(double Lon, double Lat, double RadiusMeters) : IRequest<FeatureCollection>;

public class GetAssetsNearbyQueryHandler : IRequestHandler<GetAssetsNearbyQuery, FeatureCollection>
{
    private readonly ITenantDbContext _dbContext;

    public GetAssetsNearbyQueryHandler(ITenantDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<FeatureCollection> Handle(GetAssetsNearbyQuery request, CancellationToken cancellationToken)
    {
        if (request.RadiusMeters > 50000)
        {
            throw new ArgumentException("Radius exceeds maximum allowed (50km).");
        }

        var geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        var point = geometryFactory.CreatePoint(new Coordinate(request.Lon, request.Lat));

        // En SQL Server con columna Geography, la distancia es en metros.
        var assets = await _dbContext.Assets
            .Where(a => a.Geo != null && a.Geo.Distance(point) <= request.RadiusMeters)
            .OrderBy(a => a.Geo!.Distance(point))
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
