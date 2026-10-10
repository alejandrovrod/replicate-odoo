using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AssetHub.Infrastructure.Billing;

public class PlanLimitsFilter : IAsyncActionFilter
{
    private readonly IPlatformDbContext _platformDb;
    private readonly string[] _requiredModules;
    private readonly string? _limitToCheck;

    public PlanLimitsFilter(IPlatformDbContext platformDb, string[] requiredModules, string? limitToCheck = null)
    {
        _platformDb = platformDb;
        _requiredModules = requiredModules;
        _limitToCheck = limitToCheck;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var tenantResolver = context.HttpContext.RequestServices.GetRequiredService<ITenantResolver>();
        var tenant = tenantResolver.GetCurrentTenant();

        if (tenant == null)
        {
            await next();
            return;
        }

        // 1. Validar módulos habilitados desde el plan del tenant.
        var enabledModules = await GetEnabledModulesAsync(tenant);

        foreach (var req in _requiredModules)
        {
            if (!enabledModules.Contains(req))
            {
                context.Result = new ObjectResult(new
                {
                    code = "module_not_enabled",
                    title = $"El módulo '{req}' no está habilitado."
                }) { StatusCode = 403 };
                return;
            }
        }

        // 2. Validar límite (ej. MaxUsers, MaxAssets) solo en endpoints de creación (POST)
        if (!string.IsNullOrEmpty(_limitToCheck))
        {
            var usageTracker = context.HttpContext.RequestServices.GetRequiredService<IUsageTracker>();
            
            if (_limitToCheck == "MaxUsers")
            {
                var users = await usageTracker.GetCurrentUsersCountAsync();
                var maxUsers = 10; // Dummy
                if (users >= maxUsers)
                {
                    context.Result = new ObjectResult(new
                    {
                        code = "plan_limit_exceeded",
                        title = "Límite de usuarios excedido."
                    }) { StatusCode = 402 };
                    return;
                }
            }
        }

        await next();
    }

    private async Task<IEnumerable<string>> GetEnabledModulesAsync(Tenant tenant)
    {
        if (tenant.PlanId == Guid.Empty)
        {
            // TODO-M4: Remove this fallback once M4 (Payments & Subscriptions) is implemented.
            // Tenants without a PlanId should not exist after signup assigns a default plan.
            // Fallback for unassigned tenants: allow all operational modules.
            return new[] { "core", "maintenance", "incidents", "preventive-plans", "tasks", "employees", "geo", "reports" };
        }

        var plan = await _platformDb.Plans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == tenant.PlanId);
        if (plan == null || string.IsNullOrWhiteSpace(plan.EnabledModules))
        {
            return Enumerable.Empty<string>();
        }

        return JsonSerializer.Deserialize<List<string>>(plan.EnabledModules) ?? new List<string>();
    }
}

// Atributo para usar en controladores
public class RequirePlanLimitsAttribute : TypeFilterAttribute
{
    public RequirePlanLimitsAttribute(string requiredModule, string? limitToCheck = null) 
        : base(typeof(PlanLimitsFilter))
    {
        Arguments = limitToCheck == null 
            ? new object[] { new[] { requiredModule } } 
            : new object[] { new[] { requiredModule }, limitToCheck };
    }
}
