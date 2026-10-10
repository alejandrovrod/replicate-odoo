using System;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Tasks.Commands;

public class AddTaskEvidenceCommand : IRequest<Guid>
{
    public Guid WorkTaskId { get; set; }
    
    public string Type { get; set; } = string.Empty; // photo, signature, note, geocheck
    
    public string? BlobUri { get; set; }
    public string? Note { get; set; }
    
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

public class AddTaskEvidenceCommandHandler : IRequestHandler<AddTaskEvidenceCommand, Guid>
{
    private readonly ITenantDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantResolver _tenantResolver;

    public AddTaskEvidenceCommandHandler(ITenantDbContext db, ICurrentUser currentUser, ITenantResolver tenantResolver)
    {
        _db = db;
        _currentUser = currentUser;
        _tenantResolver = tenantResolver;
    }

    public async Task<Guid> Handle(AddTaskEvidenceCommand request, CancellationToken cancellationToken)
    {
        var taskExists = await _db.WorkTasks.AnyAsync(t => t.Id == request.WorkTaskId, cancellationToken);
        if (!taskExists)
            throw new ArgumentException("Tarea no encontrada");

        if (request.Type == "geocheck")
        {
            if (!request.Latitude.HasValue || !request.Longitude.HasValue)
                throw new ArgumentException("La evidencia Geocheck requiere latitud y longitud");
        }
        else if (request.Type == "photo")
        {
            if (string.IsNullOrWhiteSpace(request.BlobUri))
                throw new ArgumentException("La evidencia de foto requiere un BlobUri");
        }

        var evidence = new TaskEvidence
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantResolver.GetCurrentTenantId()!.Value,
            WorkTaskId = request.WorkTaskId,
            Type = request.Type,
            BlobUri = request.BlobUri,
            Note = request.Note,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            CapturedByUserId = _currentUser.Id!.Value,
            CapturedAt = DateTime.UtcNow
        };

        _db.TaskEvidences.Add(evidence);
        await _db.SaveChangesAsync(cancellationToken);

        return evidence.Id;
    }
}
