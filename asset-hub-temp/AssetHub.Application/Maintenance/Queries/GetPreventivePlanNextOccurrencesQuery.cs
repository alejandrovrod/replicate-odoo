using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using Cronos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Maintenance.Queries;

public class GetPreventivePlanNextOccurrencesQuery : IRequest<GetPreventivePlanNextOccurrencesResult>
{
    public Guid PlanId { get; set; }
    public int Count { get; set; } = 12;
}

public class GetPreventivePlanNextOccurrencesResult
{
    public List<DateTime> Items { get; set; } = new();
}

public class GetPreventivePlanNextOccurrencesQueryHandler
    : IRequestHandler<GetPreventivePlanNextOccurrencesQuery, GetPreventivePlanNextOccurrencesResult>
{
    private readonly ITenantDbContext _db;

    public GetPreventivePlanNextOccurrencesQueryHandler(ITenantDbContext db)
    {
        _db = db;
    }

    public async Task<GetPreventivePlanNextOccurrencesResult> Handle(
        GetPreventivePlanNextOccurrencesQuery request,
        CancellationToken cancellationToken)
    {
        var plan = await _db.PreventivePlans
            .FirstOrDefaultAsync(p => p.Id == request.PlanId, cancellationToken);

        if (plan == null)
        {
            throw new ArgumentException($"Plan preventivo '{request.PlanId}' no encontrado.");
        }

        var result = new GetPreventivePlanNextOccurrencesResult();

        if (!plan.IsActive || string.IsNullOrEmpty(plan.CronExpression))
        {
            return result;
        }

        try
        {
            var expression = CronExpression.Parse(plan.CronExpression);
            var from = plan.NextRunAt ?? DateTime.UtcNow;

            // Start from one tick before NextRunAt so it's included
            var cursor = from.AddSeconds(-1);
            var tz = AssetHub.Application.Common.Time.TimeHelper.GetMexicoCityTimeZone();

            for (int i = 0; i < request.Count; i++)
            {
                var next = expression.GetNextOccurrence(cursor, tz);
                if (next == null) break;
                if (plan.EndsAt.HasValue && next.Value > plan.EndsAt.Value) break;

                result.Items.Add(next.Value);
                cursor = next.Value;
            }
        }
        catch
        {
            // Invalid cron expression — return empty
        }

        return result;
    }
}
