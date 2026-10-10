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
/// Agrega una nueva version (con sus traducciones) a una plantilla existente.
/// No modifica la version activa; usar ActivateCommunicationTemplateCommand.
/// </summary>
public class AddCommunicationTemplateVersionCommand : IRequest<Guid>
{
    public Guid TemplateId { get; set; }

    public List<TranslationInput> Translations { get; set; } = new();

    public class TranslationInput
    {
        public string Locale { get; set; } = string.Empty;
        public string? Subject { get; set; }
        public string Content { get; set; } = string.Empty;
        public string? DesignJson { get; set; }
    }
}

public class AddCommunicationTemplateVersionCommandHandler : IRequestHandler<AddCommunicationTemplateVersionCommand, Guid>
{
    private readonly ITenantDbContext _db;
    private readonly ITenantResolver _tenantResolver;

    public AddCommunicationTemplateVersionCommandHandler(ITenantDbContext db, ITenantResolver tenantResolver)
    {
        _db = db;
        _tenantResolver = tenantResolver;
    }

    public async Task<Guid> Handle(AddCommunicationTemplateVersionCommand request, CancellationToken cancellationToken)
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
            .FirstOrDefaultAsync(t => t.Id == request.TemplateId && t.TenantId == tenantId, cancellationToken);

        if (template == null)
            throw new ArgumentException("Plantilla no encontrada.");

        var nextVersionNumber = template.Versions.Any()
            ? template.Versions.Max(v => v.VersionNumber) + 1
            : 1;

        var version = new CommunicationTemplateVersion
        {
            Id = Guid.NewGuid(),
            TemplateId = template.Id,
            VersionNumber = nextVersionNumber,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var t in request.Translations)
        {
            var locale = (t.Locale ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(locale))
                throw new ArgumentException("Cada traducción debe especificar un idioma.");
            if (string.IsNullOrWhiteSpace(t.Content))
                throw new ArgumentException("El contenido de la traducción es obligatorio.");

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

        _db.CommunicationTemplateVersions.Add(version);
        await _db.SaveChangesAsync(cancellationToken);

        return version.Id;
    }
}
