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
/// Crea una plantilla con su primera version y traduccion. La version creada
/// queda como activa automaticamente.
/// </summary>
public class CreateCommunicationTemplateCommand : IRequest<Guid>
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public CommunicationEntityScope EntityScope { get; set; }
    public CommunicationTemplateType TemplateType { get; set; }

    public List<TranslationInput> Translations { get; set; } = new();

    public class TranslationInput
    {
        public string Locale { get; set; } = string.Empty;
        public string? Subject { get; set; }
        public string Content { get; set; } = string.Empty;
        public string? DesignJson { get; set; }
    }
}

public class CreateCommunicationTemplateCommandHandler : IRequestHandler<CreateCommunicationTemplateCommand, Guid>
{
    private readonly ITenantDbContext _db;
    private readonly ITenantResolver _tenantResolver;

    public CreateCommunicationTemplateCommandHandler(ITenantDbContext db, ITenantResolver tenantResolver)
    {
        _db = db;
        _tenantResolver = tenantResolver;
    }

    public async Task<Guid> Handle(CreateCommunicationTemplateCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId()
            ?? throw new UnauthorizedAccessException("Tenant is required.");

        var code = (request.Code ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(code))
            throw new ArgumentException("El código de la plantilla es obligatorio.");

        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("El nombre de la plantilla es obligatorio.");

        if (request.Translations == null || request.Translations.Count == 0)
            throw new ArgumentException("La plantilla debe incluir al menos una traducción.");

        var duplicates = request.Translations
            .GroupBy(t => (t.Locale ?? string.Empty).Trim())
            .Where(g => g.Count() > 1)
            .ToList();
        if (duplicates.Count > 0)
            throw new ArgumentException($"Hay idiomas duplicados: {string.Join(", ", duplicates.Select(g => g.Key))}");

        if (await _db.CommunicationTemplates.AnyAsync(t => t.TenantId == tenantId && t.Code == code, cancellationToken))
            throw new InvalidOperationException($"Ya existe una plantilla con el código '{code}' en este tenant.");

        var translations = request.Translations
            .Select(t => new CommunicationTemplateTranslation
            {
                Id = Guid.NewGuid(),
                Locale = (t.Locale ?? string.Empty).Trim(),
                Subject = t.Subject,
                Content = t.Content,
                DesignJson = t.DesignJson
            })
            .ToList();

        foreach (var tr in translations)
        {
            if (string.IsNullOrEmpty(tr.Locale))
                throw new ArgumentException("Cada traducción debe especificar un idioma.");
            if (string.IsNullOrWhiteSpace(tr.Content))
                throw new ArgumentException("El contenido de la traducción es obligatorio.");
        }

        var version = new CommunicationTemplateVersion
        {
            Id = Guid.NewGuid(),
            VersionNumber = 1,
            CreatedAt = DateTime.UtcNow,
            Translations = translations
        };

        var template = new CommunicationTemplate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = code,
            Name = name,
            EntityScope = request.EntityScope,
            TemplateType = request.TemplateType,
            Versions = new List<CommunicationTemplateVersion> { version }
        };

        _db.CommunicationTemplates.Add(template);
        await _db.SaveChangesAsync(cancellationToken);

        template.ActiveVersionId = version.Id;
        await _db.SaveChangesAsync(cancellationToken);

        return template.Id;
    }
}
