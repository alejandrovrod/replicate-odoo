using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Application.Tasks.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Tasks.Queries;

public record GetWorkTaskHistoryQuery(Guid WorkTaskId) : IRequest<List<TaskStatusHistoryDto>>;

public class GetWorkTaskHistoryQueryHandler : IRequestHandler<GetWorkTaskHistoryQuery, List<TaskStatusHistoryDto>>
{
    private readonly ITenantDbContext _db;

    public GetWorkTaskHistoryQueryHandler(ITenantDbContext db)
    {
        _db = db;
    }

    public async Task<List<TaskStatusHistoryDto>> Handle(GetWorkTaskHistoryQuery request, CancellationToken cancellationToken)
    {
        var taskExists = await _db.WorkTasks.AnyAsync(t => t.Id == request.WorkTaskId, cancellationToken);
        if (!taskExists)
            throw new ArgumentException("Tarea no encontrada");

        var history = await _db.TaskStatusHistories
            .AsNoTracking()
            .Where(h => h.WorkTaskId == request.WorkTaskId)
            .OrderByDescending(h => h.ChangedAt)
            .Select(h => new TaskStatusHistoryDto
            {
                Id = h.Id,
                FromState = h.FromState,
                ToState = h.ToState,
                ChangedAt = h.ChangedAt,
                ChangedByName = null
            })
            .ToListAsync(cancellationToken);

        return history;
    }
}
