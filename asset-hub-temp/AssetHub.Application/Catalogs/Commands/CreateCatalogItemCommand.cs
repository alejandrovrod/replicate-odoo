using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Catalogs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Catalogs.Commands;

public record CreateCatalogItemCommand(string CatalogCode, string Code, string DefaultLabel, int Order, Dictionary<string, string> Translations, Guid? ParentItemId = null) : IRequest<Guid>;

public class CreateCatalogItemCommandHandler : IRequestHandler<CreateCatalogItemCommand, Guid>
{
    private readonly ITenantDbContext _dbContext;
    private readonly ITenantResolver _tenantResolver;

    public CreateCatalogItemCommandHandler(ITenantDbContext dbContext, ITenantResolver tenantResolver)
    {
        _dbContext = dbContext;
        _tenantResolver = tenantResolver;
    }

    public async Task<Guid> Handle(CreateCatalogItemCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId();

        // Buscar catálogo (puede ser del tenant o global)
        var catalog = await _dbContext.Catalogs.FirstOrDefaultAsync(c => c.Code == request.CatalogCode, cancellationToken);
        if (catalog == null)
            throw new InvalidOperationException($"Catálogo no encontrado: {request.CatalogCode}");

        var exists = await _dbContext.CatalogItems.AnyAsync(ci => ci.CatalogId == catalog.Id && ci.Code == request.Code && ci.TenantId == tenantId, cancellationToken);
        if (exists)
            throw new InvalidOperationException($"El código de ítem {request.Code} ya existe para este tenant.");

        var item = new CatalogItem
        {
            CatalogId = catalog.Id,
            TenantId = tenantId,
            Code = request.Code,
            Order = request.Order,
            ParentItemId = request.ParentItemId
        };

        // Agregar fallback como "es"
        item.Translations.Add(new CatalogItemTranslation { Locale = "es", Label = request.DefaultLabel });

        if (request.Translations != null)
        {
            foreach (var t in request.Translations)
            {
                if (t.Key != "es") 
                    item.Translations.Add(new CatalogItemTranslation { Locale = t.Key, Label = t.Value });
            }
        }

        _dbContext.CatalogItems.Add(item);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return item.Id;
    }
}
