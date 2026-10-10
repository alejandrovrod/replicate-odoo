using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Assets;
using AssetHub.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NJsonSchema;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace AssetHub.Application.Assets.Commands;

public record CreateAssetCommand(Guid AssetTemplateId, Guid? ParentId, string Code, string Name, DateTime? InstalledAt, DateTime? CommissionedAt, decimal? ConditionIndex, string PropertiesJson, string? GeoJson) : IRequest<Guid>;

public class CreateAssetCommandHandler : IRequestHandler<CreateAssetCommand, Guid>
{
    private readonly ITenantDbContext _dbContext;
    private readonly ITenantResolver _tenantResolver;
    private readonly IAssetHierarchyService _hierarchyService;

    public CreateAssetCommandHandler(ITenantDbContext dbContext, ITenantResolver tenantResolver, IAssetHierarchyService hierarchyService)
    {
        _dbContext = dbContext;
        _tenantResolver = tenantResolver;
        _hierarchyService = hierarchyService;
    }

    public async Task<Guid> Handle(CreateAssetCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId().Value;

        // Validar límite (R4) - en un sistema real usaríamos IUsageTracker
        // Simplificado para el ejemplo
        
        var template = await _dbContext.AssetTemplates
            .FirstOrDefaultAsync(t => t.Id == request.AssetTemplateId && t.TenantId == tenantId && t.IsActive, cancellationToken);
        if (template == null) throw new InvalidOperationException("Template no encontrado o inactivo.");

        // Validar Parent
        if (request.ParentId.HasValue)
        {
            var parent = await _dbContext.Assets.Include(a => a.AssetTemplate).FirstOrDefaultAsync(a => a.Id == request.ParentId.Value, cancellationToken);
            if (parent == null) throw new InvalidOperationException("Activo padre no encontrado.");
            
            // Validar si el template del padre admite a este hijo
            if (parent.AssetTemplate != null && parent.AssetTemplate.AllowedChildTemplateIds.Any() && !parent.AssetTemplate.AllowedChildTemplateIds.Contains(request.AssetTemplateId))
            {
                throw new InvalidOperationException("Jerarquía no permitida por el template.");
            }
        }

        // Validar Code único
        var codeExists = await _dbContext.Assets.AnyAsync(a => a.Code == request.Code && a.TenantId == tenantId, cancellationToken);
        if (codeExists) throw new InvalidOperationException($"El código {request.Code} ya existe.");

        var asset = new Asset
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AssetTemplateId = request.AssetTemplateId,
            ParentId = request.ParentId,
            Code = request.Code,
            Name = request.Name,
            State = template.LifecycleStates.InitialState,
            InstalledAt = request.InstalledAt,
            CommissionedAt = request.CommissionedAt,
            ConditionIndex = request.ConditionIndex,
            PropertiesJson = request.PropertiesJson,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
            // Path se computa en el hierarchy service
        };

        // Parsear Geo
        if (!string.IsNullOrEmpty(request.GeoJson))
        {
            var reader = new GeoJsonReader();
            var geometry = reader.Read<Geometry>(request.GeoJson);
            if (geometry.GeometryType != "Point" && geometry.GeometryType != "LineString" && geometry.GeometryType != "Polygon")
            {
                throw new InvalidOperationException("GeoJson must be Point, LineString or Polygon.");
            }
            
            // Validate WGS84 - we default to SRID 4326 if parsing geojson
            geometry.SRID = 4326;
            
            asset.Geo = geometry;
            asset.GeoType = geometry.GeometryType;
        }

        // Validar EAV contra SchemaJson
        if (!string.IsNullOrEmpty(template.SchemaJson) && !string.IsNullOrEmpty(request.PropertiesJson))
        {
            var schema = await JsonSchema.FromJsonAsync(template.SchemaJson, cancellationToken);
            var errors = schema.Validate(request.PropertiesJson);
            if (errors.Count > 0)
            {
                var errorMessages = string.Join(", ", errors.Select(e => $"{e.Path}: {e.Kind}"));
                throw new InvalidOperationException($"El JSON no cumple con el esquema: {errorMessages}");
            }
        }
        else if (string.IsNullOrEmpty(request.PropertiesJson))
        {
            asset.PropertiesJson = "{}";
        }
        
        _dbContext.Assets.Add(asset);

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Calcular Path y Hierarchy
        await _hierarchyService.InsertAssetHierarchyAsync(asset.Id, asset.ParentId, cancellationToken);
        
        // Guardar estado inicial en LifecycleEvents
        _dbContext.AssetLifecycleEvents.Add(new AssetLifecycleEvent
        {
            AssetId = asset.Id,
            EventType = "alta",
            ToState = asset.State,
            At = DateTime.UtcNow,
            UserId = Guid.Empty // Debería venir del contexto de auth
        });
        await _dbContext.SaveChangesAsync(cancellationToken);

        return asset.Id;
    }
}
