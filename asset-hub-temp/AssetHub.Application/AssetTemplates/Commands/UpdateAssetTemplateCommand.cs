using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.AssetTemplates;
using AssetHub.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NJsonSchema;
using System.Text.Json;

namespace AssetHub.Application.AssetTemplates.Commands;

public record UpdateAssetTemplateCommand(Guid Id, string Name, string Description, string SchemaJson, List<Guid> AllowedChildTemplateIds, LifecycleConfig LifecycleStates, string MaintenanceChecklist, bool CreateNewVersion = false) : IRequest<Guid>;

public class UpdateAssetTemplateCommandHandler : IRequestHandler<UpdateAssetTemplateCommand, Guid>
{
    private readonly ITenantDbContext _dbContext;
    private readonly IAssetTemplateUsageChecker _usageChecker;

    public UpdateAssetTemplateCommandHandler(ITenantDbContext dbContext, IAssetTemplateUsageChecker usageChecker)
    {
        _dbContext = dbContext;
        _usageChecker = usageChecker;
    }

    public async Task<Guid> Handle(UpdateAssetTemplateCommand request, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.AssetTemplates
            .FirstOrDefaultAsync(t => t.Id == request.Id && t.IsActive, cancellationToken);

        if (existing == null) throw new InvalidOperationException("Template no encontrado.");

        try
        {
            var schema = await JsonSchema.FromJsonAsync(request.SchemaJson, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new InvalidTemplateSchemaException(ex.Message);
        }

        await ValidateCatalogsAsync(request.SchemaJson, existing.TenantId, cancellationToken);

        if (request.LifecycleStates == null || string.IsNullOrWhiteSpace(request.LifecycleStates.InitialState))
        {
            throw new InvalidOperationException("El estado inicial de LifecycleStates es obligatorio.");
        }
        
        if (request.CreateNewVersion)
        {
            // Clonar y versionar (Solo si se solicita explicitamente)
            existing.IsActive = false; // Desactivar la versión anterior

            var clone = new AssetTemplate
            {
                TenantId = existing.TenantId,
                BusinessEntityTypeId = existing.BusinessEntityTypeId,
                Code = existing.Code, // Mantener mismo código
                Name = request.Name,
                Description = request.Description,
                SchemaJson = request.SchemaJson,
                AllowedChildTemplateIds = request.AllowedChildTemplateIds ?? new List<Guid>(),
                LifecycleStates = request.LifecycleStates,
                MaintenanceChecklist = request.MaintenanceChecklist ?? string.Empty,
                Version = existing.Version + 1,
                IsActive = true
            };

            _dbContext.AssetTemplates.Add(clone);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return clone.Id;
        }
        else
        {
            // Pisar directamente (comportamiento por defecto)
            existing.Name = request.Name;
            existing.Description = request.Description;
            existing.SchemaJson = request.SchemaJson;
            existing.AllowedChildTemplateIds = request.AllowedChildTemplateIds ?? new List<Guid>();
            existing.LifecycleStates = request.LifecycleStates;
            existing.MaintenanceChecklist = request.MaintenanceChecklist ?? string.Empty;
            
            await _dbContext.SaveChangesAsync(cancellationToken);
            return existing.Id;
        }
    }

    private async Task ValidateCatalogsAsync(string schemaJson, Guid? tenantId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(schemaJson)) return;
        
        var codes = new List<string>();
        using var doc = JsonDocument.Parse(schemaJson);
        ExtractCatalogCodes(doc.RootElement, codes);
        
        if (codes.Any())
        {
            var distinctCodes = codes.Distinct().ToList();
            var existingCatalogs = await _dbContext.Catalogs
                .Where(c => (c.TenantId == tenantId || c.IsSystem) && distinctCodes.Contains(c.Code))
                .Select(c => c.Code)
                .ToListAsync(cancellationToken);
                
            var missing = distinctCodes.Except(existingCatalogs).ToList();
            if (missing.Any())
            {
                throw new InvalidOperationException($"Los siguientes catálogos no existen o no son accesibles: {string.Join(", ", missing)}");
            }
        }
    }

    private void ExtractCatalogCodes(JsonElement element, List<string> codes)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "catalog")
            {
                if (element.TryGetProperty("catalogCode", out var codeProp))
                {
                    codes.Add(codeProp.GetString()!);
                }
            }
            
            foreach (var prop in element.EnumerateObject())
            {
                ExtractCatalogCodes(prop.Value, codes);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                ExtractCatalogCodes(item, codes);
            }
        }
    }
}
