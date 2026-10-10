using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Maintenance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Staff.Commands;

public class DeactivateEmployeeCommand : IRequest<Unit>
{
    public Guid EmployeeId { get; set; }
}

public class DeactivateEmployeeCommandHandler : IRequestHandler<DeactivateEmployeeCommand, Unit>
{
    private readonly ITenantDbContext _db;

    public DeactivateEmployeeCommandHandler(ITenantDbContext db)
    {
        _db = db;
    }

    public async Task<Unit> Handle(DeactivateEmployeeCommand request, CancellationToken cancellationToken)
    {
        var emp = await _db.Employees.FirstOrDefaultAsync(e => e.Id == request.EmployeeId, cancellationToken);
        if (emp == null)
            throw new ArgumentException("Employee not found");

        var openOrders = await _db.MaintenanceOrders
            .Where(o => o.AssignedEmployeeId == request.EmployeeId && MaintenanceOrderStates.ActiveStates.Contains(o.State))
            .Select(o => o.Id)
            .ToListAsync(cancellationToken);

        if (openOrders.Any())
        {
            throw new InvalidOperationException($"Cannot deactivate employee. Has open maintenance orders: {string.Join(", ", openOrders)}");
        }
        
        // TODO: In M14, also check open tasks

        emp.IsActive = false;
        emp.IsDeleted = true; // soft delete
        
        await _db.SaveChangesAsync(cancellationToken);
        
        return Unit.Value;
    }
}
