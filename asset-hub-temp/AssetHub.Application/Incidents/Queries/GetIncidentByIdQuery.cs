using System;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace AssetHub.Application.Incidents.Queries;

public record IncidentDetailDto(
    Guid Id,
    string Title,
    string? Description,
    Guid AssetId,
    string AssetName,
    Guid TypeId,
    Guid? PriorityId,
    Guid? WorkflowTemplateId,
    string PropertiesJson,
    string State,
    DateTime ReportedAt,
    DateTime? ResolvedAt,
    DateTime? ClosedAt,
    List<IncidentDetailDto.AttachmentDto> Attachments,
    IncidentDetailDto.MaintenanceOrderDto? MaintenanceOrder,
    List<IncidentDetailDto.WorkTaskDto> WorkTasks
)
{
    public record AttachmentDto(
        Guid Id,
        string FileUrl,
        string FileName,
        string ContentType,
        long SizeBytes
    );

    public record MaintenanceOrderDto(
        Guid Id,
        string Title,
        string State,
        Guid? AssignedEmployeeId,
        string? AssignedEmployeeName,
        DateTime? ScheduledStart,
        DateTime? ScheduledEnd
    );

    public record WorkTaskDto(
        Guid Id,
        string Title,
        string State,
        Guid? AssignedEmployeeId,
        string? AssignedEmployeeName,
        DateTime? DueAt
    );
}

public record GetIncidentByIdQuery(Guid Id) : IRequest<IncidentDetailDto?>;

public class GetIncidentByIdQueryHandler : IRequestHandler<GetIncidentByIdQuery, IncidentDetailDto?>
{
    private readonly ITenantDbContext _db;

    public GetIncidentByIdQueryHandler(ITenantDbContext db)
    {
        _db = db;
    }

    public async Task<IncidentDetailDto?> Handle(GetIncidentByIdQuery request, CancellationToken cancellationToken)
    {
        var incident = await _db.Incidents
            .Include(i => i.Asset)
            .FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken);

        if (incident == null) return null;

        var attachments = await _db.IncidentAttachments
            .Where(a => a.IncidentId == incident.Id)
            .Select(a => new IncidentDetailDto.AttachmentDto(
                a.Id,
                a.FileUrl,
                a.FileName,
                a.ContentType,
                a.SizeBytes
            ))
            .ToListAsync(cancellationToken);

        var order = await _db.MaintenanceOrders
            .Include(o => o.AssignedEmployee)
            .Where(o => o.IncidentId == incident.Id && o.Kind == "corrective")
            .Select(o => new IncidentDetailDto.MaintenanceOrderDto(
                o.Id,
                o.Title,
                o.State,
                o.AssignedEmployeeId,
                o.AssignedEmployee != null ? $"{o.AssignedEmployee.FirstName} {o.AssignedEmployee.LastName}" : null,
                o.ScheduledStart,
                o.ScheduledEnd
            ))
            .FirstOrDefaultAsync(cancellationToken);

        var tasks = await _db.WorkTasks
            .Include(t => t.AssignedEmployee)
            .Where(t => t.IncidentId == incident.Id)
            .Select(t => new IncidentDetailDto.WorkTaskDto(
                t.Id,
                t.Title,
                t.State,
                t.AssignedEmployeeId,
                t.AssignedEmployee != null ? $"{t.AssignedEmployee.FirstName} {t.AssignedEmployee.LastName}" : null,
                t.DueAt
            ))
            .ToListAsync(cancellationToken);

        return new IncidentDetailDto(
            incident.Id,
            incident.Title,
            incident.Description,
            incident.AssetId,
            incident.Asset?.Name ?? string.Empty,
            incident.TypeId,
            incident.PriorityId,
            incident.WorkflowTemplateId,
            incident.PropertiesJson,
            incident.State,
            incident.ReportedAt,
            incident.ResolvedAt,
            incident.ClosedAt,
            attachments,
            order,
            tasks
        );
    }
}
