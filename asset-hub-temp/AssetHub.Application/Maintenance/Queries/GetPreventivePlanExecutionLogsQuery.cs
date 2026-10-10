using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Maintenance.Queries;

public class GetPreventivePlanExecutionLogsQuery : IRequest<GetPreventivePlanExecutionLogsResult>
{
    public Guid PlanId { get; set; }
    public Guid? AssetId { get; set; }
    public string? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class GetPreventivePlanExecutionLogsResult
{
    public List<ExecutionLogDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
}

public class ExecutionLogDto
{
    public Guid Id { get; set; }
    public DateTime ExecutedAt { get; set; }
    public DateTime Occurrence { get; set; }
    public Guid AssetId { get; set; }
    public string? AssetName { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? GeneratedEntityType { get; set; }
    public Guid? GeneratedEntityId { get; set; }
    public string? Message { get; set; }
}

public class GetPreventivePlanExecutionLogsQueryHandler
    : IRequestHandler<GetPreventivePlanExecutionLogsQuery, GetPreventivePlanExecutionLogsResult>
{
    private readonly ITenantDbContext _db;

    public GetPreventivePlanExecutionLogsQueryHandler(ITenantDbContext db)
    {
        _db = db;
    }

    public async Task<GetPreventivePlanExecutionLogsResult> Handle(
        GetPreventivePlanExecutionLogsQuery request,
        CancellationToken cancellationToken)
    {
        var query = _db.PreventivePlanExecutionLogs
            .Where(l => l.PreventivePlanId == request.PlanId)
            .AsQueryable();

        if (request.AssetId.HasValue)
        {
            query = query.Where(l => l.AssetId == request.AssetId.Value);
        }

        if (!string.IsNullOrEmpty(request.Status))
        {
            query = query.Where(l => l.Status == request.Status);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(l => l.ExecutedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Join(
                _db.Assets.IgnoreQueryFilters(),
                log => log.AssetId,
                asset => asset.Id,
                (log, asset) => new ExecutionLogDto
                {
                    Id = log.Id,
                    ExecutedAt = log.ExecutedAt,
                    Occurrence = log.Occurrence,
                    AssetId = log.AssetId,
                    AssetName = asset.Name,
                    Status = log.Status,
                    GeneratedEntityType = log.GeneratedEntityType,
                    GeneratedEntityId = log.GeneratedEntityId,
                    Message = log.Message
                })
            .ToListAsync(cancellationToken);

        return new GetPreventivePlanExecutionLogsResult
        {
            Items = items,
            TotalCount = totalCount
        };
    }
}
