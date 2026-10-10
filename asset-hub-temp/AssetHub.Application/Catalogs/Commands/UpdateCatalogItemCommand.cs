using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Catalogs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Catalogs.Commands;

public record UpdateCatalogItemCommand(string CatalogCode, string Code, string? NewCode, string DefaultLabel, int Order, Dictionary<string, string> Translations, Guid? ParentItemId = null) : IRequest<bool>;

public class UpdateCatalogItemCommandHandler : IRequestHandler<UpdateCatalogItemCommand, bool>
{
    private readonly ITenantDbContext _dbContext;
    private readonly ITenantResolver _tenantResolver;

    public UpdateCatalogItemCommandHandler(ITenantDbContext dbContext, ITenantResolver tenantResolver)
    {
        _dbContext = dbContext;
        _tenantResolver = tenantResolver;
    }

    public async Task<bool> Handle(UpdateCatalogItemCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId();

        var catalog = await _dbContext.Catalogs.FirstOrDefaultAsync(c => c.Code == request.CatalogCode, cancellationToken);
        if (catalog == null)
            throw new InvalidOperationException($"Catálogo no encontrado: {request.CatalogCode}");

        var item = await _dbContext.CatalogItems
            .Include(ci => ci.Translations)
            .FirstOrDefaultAsync(ci => ci.CatalogId == catalog.Id && ci.Code == request.Code && ci.TenantId == tenantId, cancellationToken);

        if (item == null)
            throw new InvalidOperationException($"El código de ítem {request.Code} no existe para este catálogo y tenant.");

        item.Order = request.Order;
        item.ParentItemId = request.ParentItemId;
        
        if (!string.IsNullOrWhiteSpace(request.NewCode) && item.Code != request.NewCode)
        {
            var codeExists = await _dbContext.CatalogItems.AnyAsync(ci => ci.CatalogId == catalog.Id && ci.Code == request.NewCode && ci.TenantId == tenantId, cancellationToken);
            if (codeExists)
                throw new InvalidOperationException($"Ya existe un ítem con el código {request.NewCode} en este catálogo.");
                
            item.Code = request.NewCode;
        }

        var existingTranslations = item.Translations.ToList();

        var requestTranslations = request.Translations ?? new Dictionary<string, string>();
        if (!requestTranslations.ContainsKey("es"))
            requestTranslations["es"] = request.DefaultLabel;

        foreach (var kvp in requestTranslations)
        {
            var existing = existingTranslations.FirstOrDefault(t => t.Locale == kvp.Key);
            if (existing != null)
            {
                existing.Label = kvp.Value;
                existingTranslations.Remove(existing);
            }
            else
            {
                item.Translations.Add(new CatalogItemTranslation { Locale = kvp.Key, Label = kvp.Value });
            }
        }

        // Delete translations that are no longer in the request
        if (existingTranslations.Any())
        {
            _dbContext.CatalogItemTranslations.RemoveRange(existingTranslations);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
