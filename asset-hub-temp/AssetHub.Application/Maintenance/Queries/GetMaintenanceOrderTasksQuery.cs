using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Application.Maintenance.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Maintenance.Queries;

public record GetMaintenanceOrderTasksQuery(Guid MaintenanceOrderId) : IRequest<List<MaintenanceOrderTaskSummaryDto>>;

public class GetMaintenanceOrderTasksQueryHandler : IRequestHandler<GetMaintenanceOrderTasksQuery, List<MaintenanceOrderTaskSummaryDto>>
{
    private readonly ITenantDbContext _db;

    public GetMaintenanceOrderTasksQueryHandler(ITenantDbContext db)
    {
        _db = db;
    }

    public async Task<List<MaintenanceOrderTaskSummaryDto>> Handle(GetMaintenanceOrderTasksQuery request, CancellationToken cancellationToken)
    {
        var tasks = await _db.WorkTasks
            .AsNoTracking()
            .Include(t => t.AssignedEmployee)
            .Where(t => t.MaintenanceOrderId == request.MaintenanceOrderId && !t.IsDeleted)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new MaintenanceOrderTaskSummaryDto
            {
                Id = t.Id,
                Title = t.Title,
                State = t.State,
                AssignedEmployeeId = t.AssignedEmployeeId,
                AssignedEmployeeName = t.AssignedEmployee != null ? $"{t.AssignedEmployee.FirstName} {t.AssignedEmployee.LastName}" : null
            })
            .ToListAsync(cancellationToken);

        return tasks;
    }
}
