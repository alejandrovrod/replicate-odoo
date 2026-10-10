using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.CommunicationTemplates.Commands;

/// <summary>
/// Marca una version existente como la activa de su plantilla.
/// </summary>
public class ActivateCommunicationTemplateVersionCommand : IRequest<Unit>
{
    public Guid TemplateId { get; set; }
    public Guid VersionId { get; set; }
}

public class ActivateCommunicationTemplateVersionCommandHandler : IRequestHandler<ActivateCommunicationTemplateVersionCommand, Unit>
{
    private readonly ITenantDbContext _db;
    private readonly ITenantResolver _tenantResolver;

    public ActivateCommunicationTemplateVersionCommandHandler(ITenantDbContext db, ITenantResolver tenantResolver)
    {
        _db = db;
        _tenantResolver = tenantResolver;
    }

    public async Task<Unit> Handle(ActivateCommunicationTemplateVersionCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId()
            ?? throw new UnauthorizedAccessException("Tenant is required.");

        var template = await _db.CommunicationTemplates
            .FirstOrDefaultAsync(t => t.Id == request.TemplateId && t.TenantId == tenantId, cancellationToken);

        if (template == null)
            throw new ArgumentException("Plantilla no encontrada.");

        var version = await _db.CommunicationTemplateVersions
            .FirstOrDefaultAsync(v => v.Id == request.VersionId && v.TemplateId == template.Id, cancellationToken);

        if (version == null)
            throw new ArgumentException("La versión no pertenece a esta plantilla.");

        template.ActiveVersionId = version.Id;
        await _db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
