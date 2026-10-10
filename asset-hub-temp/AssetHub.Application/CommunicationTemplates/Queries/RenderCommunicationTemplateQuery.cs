using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.CommunicationTemplates;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.CommunicationTemplates.Queries;

/// <summary>
/// Renderiza manualmente la version activa de una plantilla con variables
/// proporcionadas por el caller (previsualizacion / REQ-004).
/// </summary>
public class RenderCommunicationTemplateQuery : IRequest<RenderedTemplateResult?>
{
    public Guid TemplateId { get; set; }

    // Idioma de la traduccion a renderizar (default "es")
    public string Locale { get; set; } = "es";

    // Variables de prueba provistas por el caller; las no provistas renderizan vacio
    public Dictionary<string, string>? SampleVariables { get; set; }
}

public class RenderedTemplateResult
{
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;
    public List<string> AvailableVariables { get; set; } = new();
}

public class RenderCommunicationTemplateQueryHandler : IRequestHandler<RenderCommunicationTemplateQuery, RenderedTemplateResult?>
{
    private const string DefaultLocale = "es";

    private readonly ITenantDbContext _db;
    private readonly ITemplateRenderEngine _renderEngine;

    public RenderCommunicationTemplateQueryHandler(ITenantDbContext db, ITemplateRenderEngine renderEngine)
    {
        _db = db;
        _renderEngine = renderEngine;
    }

    public async Task<RenderedTemplateResult?> Handle(RenderCommunicationTemplateQuery request, CancellationToken cancellationToken)
    {
        var template = await _db.CommunicationTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.TemplateId, cancellationToken);

        if (template == null) return null;

        var version = await _db.CommunicationTemplateVersions
            .AsNoTracking()
            .Include(v => v.Translations)
            .FirstOrDefaultAsync(v => v.Id == template.ActiveVersionId, cancellationToken);

        if (version == null)
            throw new InvalidOperationException("La plantilla no tiene una versión activa.");

        var locale = string.IsNullOrWhiteSpace(request.Locale) ? DefaultLocale : request.Locale.Trim();
        var translation = version.Translations.FirstOrDefault(t => t.Locale == locale)
            ?? version.Translations.FirstOrDefault(t => t.Locale == DefaultLocale)
            ?? version.Translations.FirstOrDefault();

        if (translation == null)
            throw new InvalidOperationException("La versión activa no tiene traducciones.");

        var variables = new Dictionary<string, object>();
        if (request.SampleVariables != null)
        {
            foreach (var (key, value) in request.SampleVariables)
            {
                variables[key] = value ?? string.Empty;
            }
        }

        var subject = translation.Subject != null
            ? _renderEngine.Render(translation.Subject, variables)
            : null;
        var body = _renderEngine.Render(translation.Content, variables);

        return new RenderedTemplateResult
        {
            Subject = subject,
            Body = body,
            AvailableVariables = Rendering.TemplateVariableCatalog.ForScope(template.EntityScope).ToList()
        };
    }
}
