using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Catalogs;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Staff.Helpers;

public static class StaffCatalogDefaults
{
    public const string RoleCatalogCode = "employee-role";

    public static async Task<Guid> EnsureRoleCatalogAsync(
        ITenantDbContext db,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var existingItem = await db.CatalogItems
            .IgnoreQueryFilters()
            .Include(ci => ci.Catalog)
            .Where(ci => ci.Catalog!.Code == RoleCatalogCode
                         && (ci.TenantId == tenantId || ci.TenantId == null)
                         && !ci.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingItem != null)
        {
            return existingItem.Id;
        }

        var catalog = await db.Catalogs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Code == RoleCatalogCode && (c.TenantId == tenantId || c.TenantId == null), cancellationToken);

        if (catalog == null)
        {
            catalog = new Catalog
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = RoleCatalogCode,
                Label = "Roles de empleado",
                TargetModulesJson = JsonSerializer.Serialize(new[] { "staff" }),
                IsSystem = false
            };

            db.Catalogs.Add(catalog);
            await db.SaveChangesAsync(cancellationToken);
        }

        var item = new CatalogItem
        {
            Id = Guid.NewGuid(),
            CatalogId = catalog.Id,
            TenantId = tenantId,
            Code = "general",
            Order = 0,
            Translations =
            {
                new CatalogItemTranslation { Locale = "es", Label = "General" },
                new CatalogItemTranslation { Locale = "en", Label = "General" }
            }
        };

        db.CatalogItems.Add(item);
        await db.SaveChangesAsync(cancellationToken);

        return item.Id;
    }
}
