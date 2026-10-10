using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Catalogs;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Maintenance.Helpers;

public static class PreventivePlanCatalogDefaults
{
    public static async Task<(Guid TaskTypeCatalogItemId, Guid PriorityCatalogItemId)> EnsureDefaultCatalogsAsync(
        ITenantDbContext db,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var taskTypeItemId = await EnsureCatalogItemAsync(
            db,
            tenantId,
            catalogCode: "tasktype",
            catalogLabel: "Tipos de tarea",
            itemCode: "preventive",
            itemLabel: "Preventivo",
            new[] { "tasks", "maintenance" },
            cancellationToken);

        var priorityItemId = await EnsureCatalogItemAsync(
            db,
            tenantId,
            catalogCode: "priority",
            catalogLabel: "Prioridades",
            itemCode: "normal",
            itemLabel: "Normal",
            new[] { "tasks", "incidents", "maintenance" },
            cancellationToken);

        return (taskTypeItemId, priorityItemId);
    }

    private static async Task<Guid> EnsureCatalogItemAsync(
        ITenantDbContext db,
        Guid tenantId,
        string catalogCode,
        string catalogLabel,
        string itemCode,
        string itemLabel,
        string[] targetModules,
        CancellationToken cancellationToken)
    {
        // 1. Try to find an existing item with the requested code (tenant-specific or global).
        var existingItem = await db.CatalogItems
            .IgnoreQueryFilters()
            .Include(ci => ci.Catalog)
            .Where(ci => ci.Code == itemCode && (ci.TenantId == tenantId || ci.TenantId == null) && !ci.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingItem != null)
        {
            return existingItem.Id;
        }

        // 2. Ensure the catalog exists (tenant-specific or global).
        var catalog = await db.Catalogs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Code == catalogCode && (c.TenantId == tenantId || c.TenantId == null), cancellationToken);

        if (catalog == null)
        {
            catalog = new Catalog
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = catalogCode,
                Label = catalogLabel,
                TargetModulesJson = JsonSerializer.Serialize(targetModules),
                IsSystem = false
            };

            db.Catalogs.Add(catalog);
            // Save so the catalog Id is available for the item.
            await db.SaveChangesAsync(cancellationToken);
        }

        // 3. Create the default item.
        var item = new CatalogItem
        {
            Id = Guid.NewGuid(),
            CatalogId = catalog.Id,
            TenantId = tenantId,
            Code = itemCode,
            Order = 0,
            Translations =
            {
                new CatalogItemTranslation { Locale = "es", Label = itemLabel },
                new CatalogItemTranslation { Locale = "en", Label = itemLabel }
            }
        };

        db.CatalogItems.Add(item);
        await db.SaveChangesAsync(cancellationToken);

        return item.Id;
    }
}
