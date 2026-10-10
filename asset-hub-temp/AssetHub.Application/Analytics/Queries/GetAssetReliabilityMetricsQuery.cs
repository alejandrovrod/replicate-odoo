using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Maintenance;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace AssetHub.Application.Analytics.Queries;

public class ReliabilityMetricsDto
{
    public string Scope { get; set; } = "global";
    public Guid? AssetId { get; set; }
    public int CorrectiveOrderCount { get; set; }
    public decimal? MtbfHours { get; set; }
    public decimal? MttrRestoreHours { get; set; }
    public decimal? MttrRepairHours { get; set; }
    public string? MtbfMessage { get; set; }
    public string? MttrMessage { get; set; }
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
}

public class GetAssetReliabilityMetricsQuery : IRequest<ReliabilityMetricsDto>
{
    public Guid? AssetId { get; set; }
}

/// <summary>
/// Reliability metrics (MTBF, MTTR_restore, MTTR_repair) over corrective maintenance orders.
/// DEC-001 (failure_start): FailureOccurredAt ?? Incident.ReportedAt ?? CompletedAt ?? CreatedAt.
/// DEC-002 (repair_start): RepairStartedAt ?? ScheduledStart ?? CreatedAt.
/// Preventive orders are excluded. Soft-deleted rows are excluded by the global query filter.
/// </summary>
public class GetAssetReliabilityMetricsQueryHandler : IRequestHandler<GetAssetReliabilityMetricsQuery, ReliabilityMetricsDto>
{
    private readonly ITenantDbContext _db;
    private readonly ITenantResolver _tenantResolver;
    private readonly IMemoryCache _cache;

    public GetAssetReliabilityMetricsQueryHandler(ITenantDbContext db, ITenantResolver tenantResolver, IMemoryCache cache)
    {
        _db = db;
        _tenantResolver = tenantResolver;
        _cache = cache;
    }

    public async Task<ReliabilityMetricsDto> Handle(GetAssetReliabilityMetricsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId();
        if (tenantId == null)
            throw new InvalidOperationException("Tenant context required");

        var cacheKey = $"reliability_metrics_{tenantId.Value}_{request.AssetId?.ToString() ?? "global"}";

        if (_cache.TryGetValue(cacheKey, out ReliabilityMetricsDto? cached) && cached != null)
        {
            return cached;
        }

        var result = await ComputeMetrics(request, cancellationToken);

        _cache.Set(cacheKey, result, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60)
        });

        return result;
    }

    private async Task<ReliabilityMetricsDto> ComputeMetrics(GetAssetReliabilityMetricsQuery request, CancellationToken cancellationToken)
    {
        var query = _db.MaintenanceOrders
            .Where(o => o.Kind == MaintenanceOrderKinds.Corrective)
            .Where(o => o.CompletedAt != null);

        if (request.AssetId.HasValue)
        {
            query = query.Where(o => o.AssetId == request.AssetId.Value);
        }

        var orders = await query
            .OrderBy(o => o.CompletedAt)
            .Select(o => new CorrectiveOrderPoint
            {
                Id = o.Id,
                FailureOccurredAt = o.FailureOccurredAt,
                RepairStartedAt = o.RepairStartedAt,
                ScheduledStart = o.ScheduledStart,
                CreatedAt = o.CreatedAt,
                CompletedAt = o.CompletedAt!.Value,
                IncidentId = o.IncidentId
            })
            .ToListAsync(cancellationToken);

        var dto = new ReliabilityMetricsDto
        {
            AssetId = request.AssetId,
            Scope = request.AssetId.HasValue ? "asset" : "global",
            CorrectiveOrderCount = orders.Count
        };

        if (orders.Count == 0)
        {
            dto.MtbfMessage = "No hay suficientes datos para calcular MTBF.";
            dto.MttrMessage = "No hay suficientes datos para calcular MTTR.";
            return dto;
        }

        // DEC-001: resolve Incident.ReportedAt as secondary source for failure_start.
        var incidentIds = orders.Where(o => o.IncidentId.HasValue).Select(o => o.IncidentId!.Value).Distinct().ToList();
        var incidentReportedAt = new Dictionary<Guid, DateTime>();
        if (incidentIds.Count > 0)
        {
            incidentReportedAt = await _db.Incidents
                .Where(i => incidentIds.Contains(i.Id))
                .ToDictionaryAsync(i => i.Id, i => i.ReportedAt, cancellationToken);
        }

        foreach (var o in orders)
        {
            DateTime? incidentReported = null;
            if (o.IncidentId.HasValue)
            {
                incidentReportedAt.TryGetValue(o.IncidentId.Value, out var reported);
                incidentReported = reported;
            }

            o.FailureStart = o.FailureOccurredAt
                ?? incidentReported
                ?? o.CompletedAt;

            o.RepairStart = o.RepairStartedAt ?? o.ScheduledStart ?? o.CreatedAt;
        }

        // MTBF: average time between consecutive failure starts.
        var failureStarts = orders.Select(o => o.FailureStart).OrderBy(d => d).ToList();
        if (failureStarts.Count >= 2)
        {
            var gaps = failureStarts
                .Zip(failureStarts.Skip(1), (a, b) => b - a)
                .Where(g => g > TimeSpan.Zero)
                .ToList();

            if (gaps.Count > 0)
            {
                dto.MtbfHours = Math.Round((decimal)(gaps.Sum(g => g.TotalHours) / gaps.Count), 2);
            }
            else
            {
                dto.MtbfMessage = "Las fallas registradas no tienen separación temporal suficiente.";
            }
        }
        else
        {
            dto.MtbfMessage = "Se requieren al menos 2 fallas para calcular MTBF.";
        }

        // MTTR_restore: CompletedAt - failure_start (business downtime).
        // MTTR_repair: CompletedAt - repair_start (actual wrench time, DEC-002).
        var mttrRestoreValues = orders
            .Where(o => o.CompletedAt > o.FailureStart)
            .Select(o => (o.CompletedAt - o.FailureStart).TotalHours)
            .ToList();

        var mttrRepairValues = orders
            .Where(o => o.CompletedAt > o.RepairStart)
            .Select(o => (o.CompletedAt - o.RepairStart).TotalHours)
            .ToList();

        if (mttrRestoreValues.Count > 0)
        {
            dto.MttrRestoreHours = Math.Round((decimal)mttrRestoreValues.Average(), 2);
        }
        if (mttrRepairValues.Count > 0)
        {
            dto.MttrRepairHours = Math.Round((decimal)mttrRepairValues.Average(), 2);
        }

        if (dto.MttrRestoreHours == null && dto.MttrRepairHours == null)
        {
            dto.MttrMessage = "No hay reparaciones con fechas consistentes para calcular MTTR.";
        }

        return dto;
    }

    private sealed class CorrectiveOrderPoint
    {
        public Guid Id { get; set; }
        public Guid? IncidentId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ScheduledStart { get; set; }
        public DateTime? FailureOccurredAt { get; set; }
        public DateTime? RepairStartedAt { get; set; }
        public DateTime CompletedAt { get; set; }

        // Resolved coalescence results.
        public DateTime FailureStart { get; set; }
        public DateTime RepairStart { get; set; }
    }
}
