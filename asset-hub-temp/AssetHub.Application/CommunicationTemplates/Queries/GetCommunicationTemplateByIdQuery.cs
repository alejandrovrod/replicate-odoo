using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.CommunicationTemplates;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.CommunicationTemplates.Queries;

/// <summary>
/// Detalle de una plantilla: metadatos + todas sus versiones con traducciones.
/// </summary>
public class GetCommunicationTemplateByIdQuery : IRequest<CommunicationTemplateDetailDto?>
{
    public Guid TemplateId { get; set; }
}

public class CommunicationTemplateDetailDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public CommunicationEntityScope EntityScope { get; set; }
    public CommunicationTemplateType TemplateType { get; set; }
    public Guid? ActiveVersionId { get; set; }
    public List<TemplateVersionDto> Versions { get; set; } = new();
}

public class TemplateVersionDto
{
    public Guid Id { get; set; }
    public int VersionNumber { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<TemplateTranslationDto> Translations { get; set; } = new();
}

public class TemplateTranslationDto
{
    public Guid Id { get; set; }
    public string Locale { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? DesignJson { get; set; }
}

public class GetCommunicationTemplateByIdQueryHandler : IRequestHandler<GetCommunicationTemplateByIdQuery, CommunicationTemplateDetailDto?>
{
    private readonly ITenantDbContext _db;

    public GetCommunicationTemplateByIdQueryHandler(ITenantDbContext db)
    {
        _db = db;
    }

    public async Task<CommunicationTemplateDetailDto?> Handle(GetCommunicationTemplateByIdQuery request, CancellationToken cancellationToken)
    {
        var template = await _db.CommunicationTemplates
            .AsNoTracking()
            .Include(t => t.Versions)
                .ThenInclude(v => v.Translations)
            .FirstOrDefaultAsync(t => t.Id == request.TemplateId, cancellationToken);

        if (template == null) return null;

        return new CommunicationTemplateDetailDto
        {
            Id = template.Id,
            Code = template.Code,
            Name = template.Name,
            EntityScope = template.EntityScope,
            TemplateType = template.TemplateType,
            ActiveVersionId = template.ActiveVersionId,
            Versions = template.Versions
                .OrderByDescending(v => v.VersionNumber)
                .Select(v => new TemplateVersionDto
                {
                    Id = v.Id,
                    VersionNumber = v.VersionNumber,
                    CreatedAt = v.CreatedAt,
                    Translations = v.Translations
                        .OrderBy(tr => tr.Locale)
                        .Select(tr => new TemplateTranslationDto
                        {
                            Id = tr.Id,
                            Locale = tr.Locale,
                            Subject = tr.Subject,
                            Content = tr.Content,
                            DesignJson = tr.DesignJson
                        })
                        .ToList()
                })
                .ToList()
        };
    }
}
