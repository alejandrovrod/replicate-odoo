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

public record GetWorkTaskCommentsQuery(Guid WorkTaskId) : IRequest<List<TaskCommentDto>>;

public class GetWorkTaskCommentsQueryHandler : IRequestHandler<GetWorkTaskCommentsQuery, List<TaskCommentDto>>
{
    private readonly ITenantDbContext _db;

    public GetWorkTaskCommentsQueryHandler(ITenantDbContext db)
    {
        _db = db;
    }

    public async Task<List<TaskCommentDto>> Handle(GetWorkTaskCommentsQuery request, CancellationToken cancellationToken)
    {
        var taskExists = await _db.WorkTasks.AnyAsync(t => t.Id == request.WorkTaskId, cancellationToken);
        if (!taskExists)
            throw new ArgumentException("Tarea no encontrada");

        var comments = await _db.TaskComments
            .AsNoTracking()
            .Where(c => c.WorkTaskId == request.WorkTaskId)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new TaskCommentDto
            {
                Id = c.Id,
                Text = c.Text,
                CreatedAt = c.CreatedAt,
                CreatedByName = null
            })
            .ToListAsync(cancellationToken);

        return comments;
    }
}
