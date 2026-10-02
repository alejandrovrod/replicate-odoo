using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Erp.Infrastructure.Data;

/// <summary>
/// EF caches one model per context type, so a query filter built from the model-creating
/// context's <c>ITenantProvider</c> freezes that first context's tenant - the classic multi-tenant
/// leak, reproduced by the Phase 1 isolation probe (scope B returned tenant A's rows). Including
/// the tenant id in the model cache key - the fix EF documents for multi-tenant query filters -
/// gives each tenant its own model, built by a context whose provider already carries that tenant.
/// </summary>
public sealed class TenantModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime) =>
        context is AppDbContext app
            ? (typeof(AppDbContext), app.CurrentTenantId, designTime)
            : (context.GetType(), designTime);
}
