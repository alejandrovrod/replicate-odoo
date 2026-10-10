using System;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Tasks.Commands;

public class UpdateWorkTaskCommand : IRequest
{
    public Guid WorkTaskId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? DueAt { get; set; }
    public Guid TaskTypeCatalogItemId { get; set; }
    public Guid PriorityCatalogItemId { get; set; }
    public Guid? AssignedEmployeeId { get; set; }
    public Guid? AssignedTeamId { get; set; }
    public string? PropertiesJson { get; set; }
}

public class UpdateWorkTaskCommandHandler : IRequestHandler<UpdateWorkTaskCommand>
{
    private readonly ITenantDbContext _db;

    public UpdateWorkTaskCommandHandler(ITenantDbContext db)
    {
        _db = db;
    }

    public async Task Handle(UpdateWorkTaskCommand request, CancellationToken cancellationToken)
    {
        var task = await _db.WorkTasks.FirstOrDefaultAsync(t => t.Id == request.WorkTaskId, cancellationToken);
        if (task == null)
            throw new ArgumentException("Tarea no encontrada");

        if (WorkTaskStates.TerminalStates.Contains(task.State))
            throw new InvalidOperationException("No se puede editar una tarea en un estado terminal");

        if (request.AssignedEmployeeId.HasValue)
        {
            var emp = await _db.Employees.FirstOrDefaultAsync(e => e.Id == request.AssignedEmployeeId.Value, cancellationToken);
            if (emp == null || !emp.IsActive)
                throw new ArgumentException("Empleado asignado no encontrado o inactivo");
        }

        if (request.AssignedTeamId.HasValue)
        {
            var team = await _db.Teams.FirstOrDefaultAsync(t => t.Id == request.AssignedTeamId.Value, cancellationToken);
            if (team == null || team.IsDeleted)
                throw new ArgumentException("Equipo asignado no encontrado");
        }

        task.Title = request.Title;
        task.Description = request.Description;
        task.DueAt = request.DueAt;
        task.TaskTypeCatalogItemId = request.TaskTypeCatalogItemId;
        task.PriorityCatalogItemId = request.PriorityCatalogItemId;
        task.AssignedEmployeeId = request.AssignedEmployeeId;
        task.AssignedTeamId = request.AssignedTeamId;
        if (request.PropertiesJson != null)
            task.PropertiesJson = request.PropertiesJson;

        await _db.SaveChangesAsync(cancellationToken);
    }
}
