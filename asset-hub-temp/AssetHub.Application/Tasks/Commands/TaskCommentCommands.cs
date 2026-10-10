using System;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Tasks.Commands;

public class AddTaskCommentCommand : IRequest<Guid>
{
    public Guid WorkTaskId { get; set; }
    public string Text { get; set; } = string.Empty;
}

public class AddTaskCommentCommandHandler : IRequestHandler<AddTaskCommentCommand, Guid>
{
    private readonly ITenantDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantResolver _tenantResolver;

    public AddTaskCommentCommandHandler(ITenantDbContext db, ICurrentUser currentUser, ITenantResolver tenantResolver)
    {
        _db = db;
        _currentUser = currentUser;
        _tenantResolver = tenantResolver;
    }

    public async Task<Guid> Handle(AddTaskCommentCommand request, CancellationToken cancellationToken)
    {
        var taskExists = await _db.WorkTasks.AnyAsync(t => t.Id == request.WorkTaskId, cancellationToken);
        if (!taskExists)
            throw new ArgumentException("Tarea no encontrada");

        var comment = new TaskComment
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantResolver.GetCurrentTenantId()!.Value,
            WorkTaskId = request.WorkTaskId,
            Text = request.Text,
            AuthorUserId = _currentUser.Id!.Value,
            CreatedAt = DateTime.UtcNow
        };

        _db.TaskComments.Add(comment);
        await _db.SaveChangesAsync(cancellationToken);

        return comment.Id;
    }
}

public class UpdateTaskCommentCommand : IRequest<Unit>
{
    public Guid CommentId { get; set; }
    public string Text { get; set; } = string.Empty;
}

public class UpdateTaskCommentCommandHandler : IRequestHandler<UpdateTaskCommentCommand, Unit>
{
    private readonly ITenantDbContext _db;
    private readonly ICurrentUser _currentUser;

    public UpdateTaskCommentCommandHandler(ITenantDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(UpdateTaskCommentCommand request, CancellationToken cancellationToken)
    {
        var comment = await _db.TaskComments.FirstOrDefaultAsync(c => c.Id == request.CommentId, cancellationToken);
        if (comment == null)
            throw new ArgumentException("Comentario no encontrado");

        if (comment.AuthorUserId != _currentUser.Id)
            throw new UnauthorizedAccessException("No se puede editar un comentario de otro usuario");

        comment.Text = request.Text;
        comment.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public class DeleteTaskCommentCommand : IRequest<Unit>
{
    public Guid CommentId { get; set; }
}

public class DeleteTaskCommentCommandHandler : IRequestHandler<DeleteTaskCommentCommand, Unit>
{
    private readonly ITenantDbContext _db;
    private readonly ICurrentUser _currentUser;

    public DeleteTaskCommentCommandHandler(ITenantDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(DeleteTaskCommentCommand request, CancellationToken cancellationToken)
    {
        var comment = await _db.TaskComments.FirstOrDefaultAsync(c => c.Id == request.CommentId, cancellationToken);
        if (comment == null)
            throw new ArgumentException("Comentario no encontrado");

        if (comment.AuthorUserId != _currentUser.Id)
            throw new UnauthorizedAccessException("No se puede eliminar un comentario de otro usuario");

        _db.TaskComments.Remove(comment);
        await _db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
