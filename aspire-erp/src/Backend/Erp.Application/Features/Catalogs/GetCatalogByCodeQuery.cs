using Erp.Application.Common;
using Erp.Domain.Entities.System;
using Erp.Domain.Repositories;
using Erp.Domain.Common;

namespace Erp.Application.Features.Catalogs;

public record CatalogItemDto(Guid Id, string Code, string DefaultName, string? TranslatedName, Guid CatalogId);

public record GetCatalogByCodeQuery(string Code, string LanguageCode = "en") : IQuery<List<CatalogItemDto>>;

public class GetCatalogByCodeQueryHandler : IQueryHandler<GetCatalogByCodeQuery, List<CatalogItemDto>>
{
    private readonly ICatalogRepository _repository;
    private readonly ITenantProvider _tenantProvider;
    public GetCatalogByCodeQueryHandler(ICatalogRepository repository, ITenantProvider tenantProvider)
    {
        _repository = repository;
        _tenantProvider = tenantProvider;
    }

    public async Task<List<CatalogItemDto>> HandleAsync(GetCatalogByCodeQuery request, CancellationToken cancellationToken)
    {
        var catalog = await _repository.GetCatalogByCodeAsync(request.Code, request.LanguageCode, cancellationToken);

        if (catalog == null)
        {
            if (request.Code == "ASSET_CLASSES")
            {
                var tenantId = _tenantProvider.GetCurrentTenantId();
                var newCatalog = new Catalog 
                { 
                    TenantId = tenantId, 
                    Code = "ASSET_CLASSES", 
                    Name = "Asset Classes", 
                    IsSystem = true 
                };

                var item1 = new CatalogItem { TenantId = tenantId, Code = "IT", DefaultName = "IT Equipment" };
                item1.Translations.Add(new CatalogItemTranslation { LanguageCode = "es", TranslatedName = "Equipo de TI" });
                newCatalog.Items.Add(item1);

                var item2 = new CatalogItem { TenantId = tenantId, Code = "FURNITURE", DefaultName = "Office Furniture" };
                item2.Translations.Add(new CatalogItemTranslation { LanguageCode = "es", TranslatedName = "Mobiliario de Oficina" });
                newCatalog.Items.Add(item2);

                var item3 = new CatalogItem { TenantId = tenantId, Code = "VEHICLES", DefaultName = "Vehicles" };
                item3.Translations.Add(new CatalogItemTranslation { LanguageCode = "es", TranslatedName = "Vehículos" });
                newCatalog.Items.Add(item3);

                var item4 = new CatalogItem { TenantId = tenantId, Code = "LAND", DefaultName = "Land" };
                item4.Translations.Add(new CatalogItemTranslation { LanguageCode = "es", TranslatedName = "Terrenos" });
                newCatalog.Items.Add(item4);

                _repository.AddCatalog(newCatalog);
                await _repository.SaveChangesAsync(cancellationToken);
                
                catalog = newCatalog;
                
                // Manually map to DTOs for the newly created catalog
                return catalog.Items.Select(i => new CatalogItemDto(
                    i.Id,
                    i.Code,
                    i.DefaultName,
                    i.Translations.FirstOrDefault(t => t.LanguageCode == request.LanguageCode)?.TranslatedName,
                    catalog.Id
                )).ToList();
            }
            return new List<CatalogItemDto>();
        }

        return catalog.Items.Select(i => new CatalogItemDto(
            i.Id,
            i.Code,
            i.DefaultName,
            i.Translations.FirstOrDefault()?.TranslatedName,
            i.CatalogId
        )).ToList();
    }
}
