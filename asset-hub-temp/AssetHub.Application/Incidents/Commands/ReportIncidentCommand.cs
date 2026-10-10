using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Assets.Commands;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Incidents;
using AssetHub.Application.Incidents.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AssetHub.Application.Incidents.Commands;

public class ReportIncidentCommand : IRequest<Guid>
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid AssetId { get; set; }
    public Guid TypeId { get; set; }
    public Guid? PriorityId { get; set; }
    
    public Guid? WorkflowTemplateId { get; set; }
    public string PropertiesJson { get; set; } = "{}";
    
    public string? GeoJson { get; set; }
    public string? TargetAssetState { get; set; }
    
    public List<AttachmentDto> Attachments { get; set; } = new();

    public class AttachmentDto
    {
        public string FileUrl { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
    }
}

public class ReportIncidentCommandHandler : IRequestHandler<ReportIncidentCommand, Guid>
{
    private readonly ITenantDbContext _db;
    private readonly ITenantResolver _tenantResolver;
    private readonly IMediator _mediator;
    private readonly ILogger<ReportIncidentCommandHandler> _logger;

    public ReportIncidentCommandHandler(ITenantDbContext db, ITenantResolver tenantResolver, IMediator mediator, ILogger<ReportIncidentCommandHandler> logger)
    {
        _db = db;
        _tenantResolver = tenantResolver;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<Guid> Handle(ReportIncidentCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId();
        
        var assetExists = await _db.Assets.AnyAsync(a => a.Id == request.AssetId, cancellationToken);
        if (!assetExists)
            throw new ArgumentException("Asset not found");
            
        // var typeExists = await _db.CatalogItems.AnyAsync(c => c.Id == request.TypeId, cancellationToken);
        // if (!typeExists && request.TypeId != Guid.Empty)
        //     throw new ArgumentException("Type catalog item not found");
            
        // if (request.PriorityId.HasValue && request.PriorityId.Value != Guid.Empty)
        // {
        //     var priorityExists = await _db.CatalogItems.AnyAsync(c => c.Id == request.PriorityId.Value, cancellationToken);
        //     if (!priorityExists)
        //         throw new ArgumentException("Priority catalog item not found");
        // }
        
        // Cleanup mock UUIDs from frontend
        if (request.PriorityId == Guid.Empty)
        {
            request.PriorityId = null;
        }

        if (request.TypeId == Guid.Empty)
        {
            var defaultType = await EnsureDefaultIncidentTypeAsync(tenantId.Value, cancellationToken);
            request.TypeId = defaultType.Id;
        }

        NetTopologySuite.Geometries.Geometry? geo = null;
        string? geoType = null;
        if (!string.IsNullOrWhiteSpace(request.GeoJson))
        {
            var reader = new GeoJsonReader();
            geo = reader.Read<NetTopologySuite.Geometries.Geometry>(request.GeoJson);
            if (geo != null)
            {
                geo.SRID = 4326;
                geoType = geo.GeometryType;
            }
        }

        var incident = new Incident
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Value,
            Title = request.Title,
            Description = request.Description,
            AssetId = request.AssetId,
            TypeId = request.TypeId,
            PriorityId = request.PriorityId,
            WorkflowTemplateId = request.WorkflowTemplateId,
            PropertiesJson = string.IsNullOrWhiteSpace(request.PropertiesJson) ? "{}" : request.PropertiesJson,
            State = "reported", // Podríamos obtener el initial state de la plantilla si existe
            Geo = geo,
            GeoType = geoType
        };

        if (request.WorkflowTemplateId.HasValue)
        {
            var template = await _db.WorkflowTemplates.FirstOrDefaultAsync(t => t.Id == request.WorkflowTemplateId.Value, cancellationToken);
            if (template != null && template.LifecycleStates != null && !string.IsNullOrEmpty(template.LifecycleStates.InitialState))
            {
                incident.State = template.LifecycleStates.InitialState;
            }
        }

        _db.Incidents.Add(incident);

        foreach (var att in request.Attachments)
        {
            _db.IncidentAttachments.Add(new IncidentAttachment
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId.Value,
                IncidentId = incident.Id,
                FileUrl = att.FileUrl,
                FileName = att.FileName,
                ContentType = att.ContentType,
                SizeBytes = att.SizeBytes
            });
        }

        _db.IncidentLifecycleEvents.Add(new IncidentLifecycleEvent
        {
            Id = Guid.NewGuid(),
            IncidentId = incident.Id,
            EventType = "reportado",
            FromState = string.Empty,
            ToState = incident.State,
            Notes = "Incidencia reportada",
            PropertiesJson = incident.PropertiesJson,
            At = DateTime.UtcNow,
            UserId = Guid.Empty // Sistema por ahora, hasta que inyectemos usuario
        });

        await _db.SaveChangesAsync(cancellationToken);

        await _mediator.Publish(new IncidentReportedEvent(incident.Id, incident.Title, incident.AssetId, incident.TenantId, incident.PropertiesJson), cancellationToken);

        // --- Flujo Dual: Lock the asset and propagate upward ---
        // After saving the incident, transition the affected asset to its "incidents-locked" state.
        // The resulting AssetStateChangedEvent will be handled by ParentStatePropagationHandler
        // which already implements upward propagation recursively.
        await TryLockAssetForIncidentAsync(request.AssetId, request.TargetAssetState, cancellationToken);

        return incident.Id;
    }

    private async Task<AssetHub.Domain.Catalogs.CatalogItem> EnsureDefaultIncidentTypeAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var catalog = await _db.Catalogs.FirstOrDefaultAsync(c => c.Code == "incident-types" && c.TenantId == tenantId, cancellationToken);
        if (catalog == null)
        {
            catalog = new AssetHub.Domain.Catalogs.Catalog
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = "incident-types",
                Label = "Tipos de Incidencias",
                IsSystem = true
            };
            _db.Catalogs.Add(catalog);
            await _db.SaveChangesAsync(cancellationToken);
        }

        var defaultType = await _db.CatalogItems.FirstOrDefaultAsync(c => c.CatalogId == catalog.Id && c.Code == "general", cancellationToken);
        if (defaultType == null)
        {
            defaultType = new AssetHub.Domain.Catalogs.CatalogItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CatalogId = catalog.Id,
                Code = "general",
                MetadataJson = "{\"color\":\"#6b7280\"}",
                Translations = new List<AssetHub.Domain.Catalogs.CatalogItemTranslation>
                {
                    new AssetHub.Domain.Catalogs.CatalogItemTranslation
                    {
                        Locale = "es",
                        Label = "General"
                    }
                }
            };
            _db.CatalogItems.Add(defaultType);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return defaultType;
    }

    private async Task TryLockAssetForIncidentAsync(Guid assetId, string? targetAssetState, CancellationToken cancellationToken)
    {
        var asset = await _db.Assets
            .Include(a => a.AssetTemplate)
            .FirstOrDefaultAsync(a => a.Id == assetId, cancellationToken);

        if (asset?.AssetTemplate?.LifecycleStates == null)
        {
            _logger.LogWarning("[ReportIncident] Asset {AssetId} has no lifecycle template. Skipping asset lock.", assetId);
            return;
        }

        var lifecycle = asset.AssetTemplate.LifecycleStates;

        // Find a state reachable from the current state that has AssociatedModule = "incidents"
        if (!lifecycle.Transitions.TryGetValue(asset.State, out var reachableStates) || reachableStates == null)
        {
            _logger.LogWarning("[ReportIncident] No transitions defined from state '{State}' for asset {AssetId}.", asset.State, assetId);
            return;
        }

        string? lockedState = null;

        if (!string.IsNullOrWhiteSpace(targetAssetState))
        {
            if (!reachableStates.Contains(targetAssetState))
            {
                _logger.LogWarning(
                    "[ReportIncident] Requested target asset state '{TargetState}' is not reachable from '{State}' for asset {AssetId}.",
                    targetAssetState, asset.State, assetId);
                throw new InvalidOperationException($"El estado '{targetAssetState}' no es alcanzable desde el estado actual del activo.");
            }

            if (!lifecycle.States.TryGetValue(targetAssetState, out var targetCfg) ||
                !string.Equals(targetCfg.AssociatedModule, "incidents", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "[ReportIncident] Requested target asset state '{TargetState}' does not have AssociatedModule='incidents' for asset {AssetId}.",
                    targetAssetState, assetId);
                throw new InvalidOperationException($"El estado '{targetAssetState}' no está configurado para delegar al módulo de incidencias.");
            }

            lockedState = targetAssetState;
        }
        else
        {
            lockedState = reachableStates.FirstOrDefault(s =>
                lifecycle.States.TryGetValue(s, out var cfg) &&
                string.Equals(cfg.AssociatedModule, "incidents", StringComparison.OrdinalIgnoreCase));
        }

        if (lockedState == null)
        {
            _logger.LogWarning(
                "[ReportIncident] No reachable state with AssociatedModule='incidents' from '{State}' for asset {AssetId}. Skipping lock.",
                asset.State, assetId);
            return;
        }

        _logger.LogInformation(
            "[ReportIncident] Locking asset {AssetId} from '{From}' → '{To}' due to new incident.",
            assetId, asset.State, lockedState);

        try
        {
            await _mediator.Send(
                new ChangeAssetEnvironmentStateCommand(assetId, lockedState, "Bloqueado automáticamente por incidencia reportada.", IsAutomatedTransition: true),
                cancellationToken);
        }
        catch (Exception ex)
        {
            // Non-fatal: log but don't fail the incident creation
            _logger.LogError(ex, "[ReportIncident] Failed to lock asset {AssetId}. Incident was still created.", assetId);
        }
    }
}
