using System;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Notifications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Tasks.Commands;

public class AssignWorkTaskCommand : IRequest<Unit>
{
    public Guid WorkTaskId { get; set; }

    public Guid? AssignedEmployeeId { get; set; }
    public Guid? AssignedTeamId { get; set; }
}

public class AssignWorkTaskCommandHandler : IRequestHandler<AssignWorkTaskCommand, Unit>
{
    private readonly ITenantDbContext _db;
    private readonly ITenantResolver _tenantResolver;

    public AssignWorkTaskCommandHandler(ITenantDbContext db, ITenantResolver tenantResolver)
    {
        _db = db;
        _tenantResolver = tenantResolver;
    }

    public async Task<Unit> Handle(AssignWorkTaskCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId()!.Value;
        var task = await _db.WorkTasks.FirstOrDefaultAsync(t => t.Id == request.WorkTaskId, cancellationToken);
        if (task == null)
            throw new ArgumentException("Tarea no encontrada");

        if (request.AssignedEmployeeId.HasValue)
        {
            var emp = await _db.Employees.FirstOrDefaultAsync(e => e.Id == request.AssignedEmployeeId.Value, cancellationToken);
            if (emp == null || !emp.IsActive)
                throw new ArgumentException("Empleado no encontrado o inactivo");
        }

        task.AssignedEmployeeId = request.AssignedEmployeeId;
        task.AssignedTeamId = request.AssignedTeamId;

        await _db.SaveChangesAsync(cancellationToken);

        // Notify assigned employee if linked to a user
        if (request.AssignedEmployeeId.HasValue)
        {
            var employee = await _db.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == request.AssignedEmployeeId.Value, cancellationToken);

            if (employee?.UserId.HasValue == true)
            {
                _db.Notifications.Add(new Notification
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    UserId = employee.UserId.Value,
                    Title = "Nueva asignaciÃ³n de tarea",
                    Message = $"Se te asignÃ³ la tarea '{task.Title}'.",
                    RelatedEntityType = "WorkTask",
                    RelatedEntityId = task.Id
                });

                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        return Unit.Value;
    }
}
