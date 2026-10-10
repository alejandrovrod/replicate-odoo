using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace AssetHub.Application.Analytics.Queries;

public class DashboardStatsDto
{
    public Dictionary<string, int> AssetsByState { get; set; } = new();
    public Dictionary<string, int> IncidentsByPriority { get; set; } = new();
    public decimal TaskCompliancePercentage { get; set; }
}

public class GetDashboardStatsQuery : IRequest<DashboardStatsDto>
{
}

public class GetDashboardStatsQueryHandler : IRequestHandler<GetDashboardStatsQuery, DashboardStatsDto>
{
    private readonly ITenantDbContext _db;
    private readonly ITenantResolver _tenantResolver;
    private readonly IMemoryCache _cache;

    public GetDashboardStatsQueryHandler(ITenantDbContext db, ITenantResolver tenantResolver, IMemoryCache cache)
    {
        _db = db;
        _tenantResolver = tenantResolver;
        _cache = cache;
    }

    public async Task<DashboardStatsDto> Handle(GetDashboardStatsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId();
        if (tenantId == null)
            throw new InvalidOperationException("Tenant context required");

        var cacheKey = $"dashboard_stats_{tenantId.Value}";

        if (_cache.TryGetValue(cacheKey, out DashboardStatsDto? cachedStats) && cachedStats != null)
        {
            return cachedStats;
        }

        var stats = new DashboardStatsDto();

        // 1. Assets by state (excluding soft deleted implicitly by global filter)
        var assetsByState = await _db.Assets
            .GroupBy(a => a.State)
            .Select(g => new { State = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        stats.AssetsByState = assetsByState.ToDictionary(a => a.State, a => a.Count);

        // 2. Incidents by priority
        var incidentsByPriority = await _db.Incidents
            .GroupBy(i => i.PriorityId)
            .Select(g => new { PriorityId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        // Map Guid to string for the DTO (we could join with CatalogItem for names but for simplicity we return string keys)
        stats.IncidentsByPriority = incidentsByPriority.ToDictionary(i => i.PriorityId?.ToString() ?? "unassigned", i => i.Count);

        // 3. Task compliance (% a tiempo vs vencidas SLA)
        // Consideramos tareas cerradas (done, cancelled) y evaluamos si CompletedAt <= DueDate
        // Si no tiene DueDate, no aplica SLA.
        var closedTasks = await _db.WorkTasks
            .Where(t => WorkTaskStates.TerminalStates.Contains(t.State))
            .Where(t => t.DueDate != null && t.CompletedAt != null)
            .Select(t => new { t.DueDate, t.CompletedAt })
            .ToListAsync(cancellationToken);

        if (closedTasks.Any())
        {
            var onTime = closedTasks.Count(t => t.CompletedAt <= t.DueDate);
            stats.TaskCompliancePercentage = Math.Round((decimal)onTime / closedTasks.Count * 100, 2);
        }
        else
        {
            stats.TaskCompliancePercentage = 100m; // No tasks with SLA
        }

        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60)
        };

        _cache.Set(cacheKey, stats, cacheOptions);

        return stats;
    }
}
