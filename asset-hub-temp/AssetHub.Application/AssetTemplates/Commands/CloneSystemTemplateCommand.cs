using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.AssetTemplates;
using AssetHub.Domain.EntityTypes;
using AssetHub.Domain.Catalogs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.AssetTemplates.Commands;

public record CloneSystemTemplateCommand(Guid SystemTemplateId) : IRequest<Guid>;

public class CloneSystemTemplateCommandHandler : IRequestHandler<CloneSystemTemplateCommand, Guid>
{
    private readonly ITenantDbContext _dbContext;
    private readonly ITenantResolver _tenantResolver;

    public CloneSystemTemplateCommandHandler(ITenantDbContext dbContext, ITenantResolver tenantResolver)
    {
        _dbContext = dbContext;
        _tenantResolver = tenantResolver;
    }

    public async Task<Guid> Handle(CloneSystemTemplateCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId();

        // 1. Fetch the system template (TenantId must be null)
        // Note: we use IgnoreQueryFilters to bypass any current tenant restrictions if necessary, 
        // though our updated filter allows null. We explicitly check TenantId == null.
        var systemTemplate = await _dbContext.AssetTemplates
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == request.SystemTemplateId && t.TenantId == null, cancellationToken);

        if (systemTemplate == null)
            throw new Exception("System template not found.");

        // 2. Fetch the category (BusinessEntityType)
        var systemCategory = await _dbContext.BusinessEntityTypes
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == systemTemplate.BusinessEntityTypeId, cancellationToken);

        Guid targetCategoryId = systemTemplate.BusinessEntityTypeId;

        if (systemCategory != null && systemCategory.TenantId == null)
        {
            // The category is also a system category. Let's see if the tenant already has a cloned version.
            var existingTenantCategory = await _dbContext.BusinessEntityTypes
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Code == systemCategory.Code, cancellationToken);

            if (existingTenantCategory != null)
            {
                targetCategoryId = existingTenantCategory.Id;
            }
            else
            {
                // Clone the category
                var newCategory = new BusinessEntityType
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Code = systemCategory.Code,
                    Name = systemCategory.Name,
                    Description = systemCategory.Description,
                    Icon = systemCategory.Icon,
                    EnabledModules = systemCategory.EnabledModules?.ToList() ?? new(),
                    DefaultCatalogIds = new List<Guid>(), // We will populate this below
                    IsActive = true
                };

                // Clone Default Catalogs if they exist
                if (systemCategory.DefaultCatalogIds != null && systemCategory.DefaultCatalogIds.Any())
                {
                    foreach (var catalogId in systemCategory.DefaultCatalogIds)
                    {
                        var systemCatalog = await _dbContext.Catalogs
                            .IgnoreQueryFilters()
                            .FirstOrDefaultAsync(c => c.Id == catalogId && c.TenantId == null, cancellationToken);

                        if (systemCatalog != null)
                        {
                            // Check if tenant already has a catalog with this code
                            var existingTenantCatalog = await _dbContext.Catalogs
                                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Code == systemCatalog.Code, cancellationToken);

                            if (existingTenantCatalog != null)
                            {
                                newCategory.DefaultCatalogIds.Add(existingTenantCatalog.Id);
                            }
                            else
                            {
                                // Clone Catalog
                                var newCatalog = new Catalog
                                {
                                    Id = Guid.NewGuid(),
                                    TenantId = tenantId,
                                    Code = systemCatalog.Code,
                                    Label = systemCatalog.Label,
                                    IsSystem = false,
                                    TargetModulesJson = systemCatalog.TargetModulesJson
                                };
                                _dbContext.Catalogs.Add(newCatalog);
                                newCategory.DefaultCatalogIds.Add(newCatalog.Id);

                                // Clone Catalog Items
                                var systemItems = await _dbContext.CatalogItems
                                    .IgnoreQueryFilters()
                                    .Where(ci => ci.CatalogId == systemCatalog.Id && ci.TenantId == null && !ci.IsDeleted)
                                    .ToListAsync(cancellationToken);

                                foreach (var item in systemItems)
                                {
                                    var newItem = new CatalogItem
                                    {
                                        Id = Guid.NewGuid(),
                                        CatalogId = newCatalog.Id,
                                        TenantId = tenantId,
                                        Code = item.Code,
                                        ParentItemId = null,
                                        Order = item.Order,
                                        MetadataJson = item.MetadataJson,
                                        IsDeleted = false
                                    };
                                    _dbContext.CatalogItems.Add(newItem);

                                    // Clone Translations
                                    var translations = await _dbContext.CatalogItemTranslations
                                        .IgnoreQueryFilters()
                                        .Where(t => t.CatalogItemId == item.Id)
                                        .ToListAsync(cancellationToken);

                                    foreach (var trans in translations)
                                    {
                                        _dbContext.CatalogItemTranslations.Add(new CatalogItemTranslation
                                        {
                                            Id = Guid.NewGuid(),
                                            CatalogItemId = newItem.Id,
                                            Locale = trans.Locale,
                                            Label = trans.Label
                                        });
                                    }
                                }
                            }
                        }
                        else 
                        {
                           newCategory.DefaultCatalogIds.Add(catalogId);
                        }
                    }
                }

                _dbContext.BusinessEntityTypes.Add(newCategory);
                targetCategoryId = newCategory.Id;
            }
        }

        // 3. Clone the template
        var newTemplate = new AssetTemplate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BusinessEntityTypeId = targetCategoryId,
            Code = systemTemplate.Code,
            Name = systemTemplate.Name,
            Description = systemTemplate.Description,
            SchemaJson = systemTemplate.SchemaJson,
            AllowedChildTemplateIds = systemTemplate.AllowedChildTemplateIds?.ToList() ?? new(),
            LifecycleStates = new LifecycleConfig 
            {
                States = systemTemplate.LifecycleStates?.States?.ToDictionary(k => k.Key, v => v.Value) ?? new(),
                Transitions = systemTemplate.LifecycleStates?.Transitions?.ToDictionary(k => k.Key, v => v.Value) ?? new(),
                InitialState = systemTemplate.LifecycleStates?.InitialState
            },
            MaintenanceChecklist = systemTemplate.MaintenanceChecklist,
            Version = 1,
            IsActive = true
        };

        _dbContext.AssetTemplates.Add(newTemplate);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return newTemplate.Id;
    }
}
