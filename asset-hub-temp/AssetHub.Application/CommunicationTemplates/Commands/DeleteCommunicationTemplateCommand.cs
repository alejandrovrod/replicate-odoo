using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.CommunicationTemplates.Commands;

/// <summary>
/// Elimina una plantilla con todas sus versiones y traducciones.
/// </summary>
public class DeleteCommunicationTemplateCommand : IRequest<Unit>
{
    public Guid TemplateId { get; set; }
}

public class DeleteCommunicationTemplateCommandHandler : IRequestHandler<DeleteCommunicationTemplateCommand, Unit>
{
    private readonly ITenantDbContext _db;
    private readonly ITenantResolver _tenantResolver;

    public DeleteCommunicationTemplateCommandHandler(ITenantDbContext db, ITenantResolver tenantResolver)
    {
        _db = db;
        _tenantResolver = tenantResolver;
    }

    public async Task<Unit> Handle(DeleteCommunicationTemplateCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId()
            ?? throw new UnauthorizedAccessException("Tenant is required.");

        var template = await _db.CommunicationTemplates
            .Include(t => t.Versions)
            .FirstOrDefaultAsync(t => t.Id == request.TemplateId && t.TenantId == tenantId, cancellationToken);

        if (template == null)
            throw new ArgumentException("Plantilla no encontrada.");

        // Break circular dependency before deleting
        template.ActiveVersionId = null;
        await _db.SaveChangesAsync(cancellationToken);

        var versionIds = template.Versions.Select(v => v.Id).ToList();

        var translations = await _db.CommunicationTemplateTranslations
            .Where(tr => versionIds.Contains(tr.VersionId))
            .ToListAsync(cancellationToken);

        _db.CommunicationTemplateTranslations.RemoveRange(translations);
        _db.CommunicationTemplateVersions.RemoveRange(template.Versions);
        _db.CommunicationTemplates.Remove(template);

        await _db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
