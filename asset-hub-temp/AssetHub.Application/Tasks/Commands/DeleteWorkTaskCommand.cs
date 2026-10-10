using System;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Tasks.Commands;

public record DeleteWorkTaskCommand(Guid WorkTaskId) : IRequest;

public class DeleteWorkTaskCommandHandler : IRequestHandler<DeleteWorkTaskCommand>
{
    private readonly ITenantDbContext _db;

    public DeleteWorkTaskCommandHandler(ITenantDbContext db)
    {
        _db = db;
    }

    public async Task Handle(DeleteWorkTaskCommand request, CancellationToken cancellationToken)
    {
        var task = await _db.WorkTasks.FirstOrDefaultAsync(t => t.Id == request.WorkTaskId, cancellationToken);
        if (task == null)
            throw new ArgumentException("Tarea no encontrada");

        task.IsDeleted = true;
        task.DeletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }
}
