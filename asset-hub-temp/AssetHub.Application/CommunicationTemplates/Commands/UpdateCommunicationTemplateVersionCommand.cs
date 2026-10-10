using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.CommunicationTemplates;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.CommunicationTemplates.Commands;

/// <summary>
/// Actualiza una versión existente (sobrescribe sus traducciones).
/// </summary>
public class UpdateCommunicationTemplateVersionCommand : IRequest<Guid>
{
    public Guid TemplateId { get; set; }
    public Guid VersionId { get; set; }

    public List<TranslationInput> Translations { get; set; } = new();

    public class TranslationInput
    {
        public string Locale { get; set; } = string.Empty;
        public string? Subject { get; set; }
        public string Content { get; set; } = string.Empty;
        public string? DesignJson { get; set; }
    }
}

public class UpdateCommunicationTemplateVersionCommandHandler : IRequestHandler<UpdateCommunicationTemplateVersionCommand, Guid>
{
    private readonly ITenantDbContext _db;
    private readonly ITenantResolver _tenantResolver;

    public UpdateCommunicationTemplateVersionCommandHandler(ITenantDbContext db, ITenantResolver tenantResolver)
    {
        _db = db;
        _tenantResolver = tenantResolver;
    }

    public async Task<Guid> Handle(UpdateCommunicationTemplateVersionCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId()
            ?? throw new UnauthorizedAccessException("Tenant is required.");

        if (request.Translations == null || request.Translations.Count == 0)
            throw new ArgumentException("La versión debe incluir al menos una traducción.");

        var duplicates = request.Translations
            .GroupBy(t => (t.Locale ?? string.Empty).Trim())
            .Where(g => g.Count() > 1)
            .ToList();
        if (duplicates.Count > 0)
            throw new ArgumentException($"Hay idiomas duplicados: {string.Join(", ", duplicates.Select(g => g.Key))}");

        var template = await _db.CommunicationTemplates
            .Include(t => t.Versions)
                .ThenInclude(v => v.Translations)
            .FirstOrDefaultAsync(t => t.Id == request.TemplateId && t.TenantId == tenantId, cancellationToken);

        if (template == null)
            throw new ArgumentException("Plantilla no encontrada.");

        var version = template.Versions.FirstOrDefault(v => v.Id == request.VersionId);
        if (version == null)
            throw new ArgumentException("Versión no encontrada en esta plantilla.");

        // Remove translations that are not in the request
        var localesInRequest = request.Translations.Select(t => (t.Locale ?? string.Empty).Trim()).ToList();
        var toRemove = version.Translations.Where(x => !localesInRequest.Contains(x.Locale)).ToList();
        foreach (var rm in toRemove)
        {
            version.Translations.Remove(rm);
            _db.CommunicationTemplateTranslations.Remove(rm);
        }

        // Update existing and add new
        foreach (var t in request.Translations)
        {
            var locale = (t.Locale ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(locale))
                throw new ArgumentException("Cada traducción debe especificar un idioma.");
            if (string.IsNullOrWhiteSpace(t.Content))
                throw new ArgumentException("El contenido de la traducción es obligatorio.");

            var existing = version.Translations.FirstOrDefault(x => x.Locale == locale);
            if (existing != null)
            {
                existing.Subject = t.Subject;
                existing.Content = t.Content;
                existing.DesignJson = t.DesignJson;
            }
            else
            {
                version.Translations.Add(new CommunicationTemplateTranslation
                {
                    Id = Guid.NewGuid(),
                    VersionId = version.Id,
                    Locale = locale,
                    Subject = t.Subject,
                    Content = t.Content,
                    DesignJson = t.DesignJson
                });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        return version.Id;
    }
}
