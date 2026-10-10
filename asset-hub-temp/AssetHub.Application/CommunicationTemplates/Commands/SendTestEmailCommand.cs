using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.CommunicationTemplates.Rendering;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.CommunicationTemplates;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.CommunicationTemplates.Commands;

/// <summary>
/// Envía un correo de prueba de una plantilla a una dirección indicada,
/// rellenando las variables con datos falsos (dummy) para que el diseño
/// se pueda validar sin generar eventos reales.
/// </summary>
public class SendTestEmailCommand : IRequest<Unit>
{
    public Guid TemplateId { get; set; }

    // Dirección de destino del correo de prueba
    public string To { get; set; } = string.Empty;

    // Idioma de la traducción a probar (fallback "es")
    public string Locale { get; set; } = "es";

    // Traducciones de override (contenido sin guardar del editor).
    // Si es null, se usa la versión activa de la plantilla.
    public List<TestTranslationInput>? Translations { get; set; }

    public class TestTranslationInput
    {
        public string Locale { get; set; } = string.Empty;
        public string? Subject { get; set; }
        public string Content { get; set; } = string.Empty;
    }
}

public class SendTestEmailCommandHandler : IRequestHandler<SendTestEmailCommand, Unit>
{
    private const string DefaultLocale = "es";

    private readonly ITenantDbContext _db;
    private readonly ITenantResolver _tenantResolver;
    private readonly ICommunicationTemplateService _templateService;
    private readonly IEmailService _emailService;

    public SendTestEmailCommandHandler(
        ITenantDbContext db,
        ITenantResolver tenantResolver,
        ICommunicationTemplateService templateService,
        IEmailService emailService)
    {
        _db = db;
        _tenantResolver = tenantResolver;
        _templateService = templateService;
        _emailService = emailService;
    }

    public async Task<Unit> Handle(SendTestEmailCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId()
            ?? throw new UnauthorizedAccessException("Tenant is required.");

        var to = (request.To ?? string.Empty).Trim();
        if (!IsValidEmail(to))
            throw new ArgumentException("Ingresá una dirección de correo válida.");

        var template = await _db.CommunicationTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.TemplateId && t.TenantId == tenantId, cancellationToken);

        if (template == null)
            throw new ArgumentException("Plantilla no encontrada.");

        if (template.TemplateType != CommunicationTemplateType.Email)
            throw new InvalidOperationException("Solo se pueden enviar correos de prueba de plantillas de tipo Email.");

        var locale = string.IsNullOrWhiteSpace(request.Locale) ? DefaultLocale : request.Locale.Trim();

        var rendered = request.Translations is { Count: > 0 }
            ? await RenderOverridesAsync(tenantId, request.Translations, locale, template.EntityScope, cancellationToken)
            : await _templateService.RenderByIdAsync(tenantId, template.Id, locale, DummyVariables(template.EntityScope), cancellationToken);

        if (rendered == null)
            throw new InvalidOperationException("La plantilla no tiene una versión activa. Guardá la plantilla antes de enviar una prueba.");

        var subject = rendered.Subject ?? $"[Prueba] {template.Name}";
        var body = InsertTestBanner(rendered.Body);

        await _emailService.SendEmailAsync(to, subject, body, isHtml: true, cancellationToken: cancellationToken);

        return Unit.Value;
    }

    private async Task<RenderedCommunication> RenderOverridesAsync(
        Guid tenantId,
        List<SendTestEmailCommand.TestTranslationInput> overrides,
        string locale,
        CommunicationEntityScope scope,
        CancellationToken cancellationToken)
    {
        var translations = overrides
            .Where(t => !string.IsNullOrWhiteSpace(t.Locale) && t.Content != null)
            .Select(t => new CommunicationTemplateTranslation
            {
                Locale = t.Locale.Trim(),
                Subject = t.Subject,
                Content = t.Content
            })
            .ToList();

        if (translations.Count == 0)
            throw new ArgumentException("Las traducciones de prueba están vacías.");

        return await _templateService.RenderTranslationsAsync(
            tenantId, translations, locale, DummyVariables(scope), cancellationToken);
    }

    /// <summary>
    /// Datos falsos por ámbito: cubren TODAS las variables del catálogo para
    /// que ninguna quede vacía en el correo de prueba.
    /// </summary>
    private static Dictionary<string, object> DummyVariables(CommunicationEntityScope scope) => scope switch
    {
        CommunicationEntityScope.Incident => new Dictionary<string, object>
        {
            ["incident.title"] = "Fuga de agua en bomba principal",
            ["incident.priority"] = "alta",
            ["incident.state"] = "reported",
            ["incident.type"] = "mecánico",
            ["incident.reportedAt"] = "2026-09-13 10:30",
            ["asset.name"] = "Bomba Centrífuga BC-200",
            ["asset.code"] = "AST-0001",
            ["recipient.name"] = "Juan Pérez"
        },
        CommunicationEntityScope.MaintenanceOrder => new Dictionary<string, object>
        {
            ["order.id"] = "ORD-000123",
            ["order.title"] = "Mantenimiento Preventivo de Bomba",
            ["order.kind"] = "preventive",
            ["order.state"] = "En Progreso",
            ["order.scheduledStart"] = "2026-09-13 08:00",
            ["order.scheduledEnd"] = "2026-09-13 12:00",
            ["asset.name"] = "Bomba Centrífuga BC-200",
            ["asset.code"] = "AST-0001",
            ["recipient.name"] = "Juan Pérez"
        },
        CommunicationEntityScope.WorkTask => new Dictionary<string, object>
        {
            ["task.id"] = "TASK-000456",
            ["task.title"] = "Reemplazo de filtro de aceite",
            ["task.state"] = "assigned",
            ["task.type"] = "TP-001",
            ["task.priority"] = "media",
            ["task.dueAt"] = "2026-09-15 17:00",
            ["asset.name"] = "Compresor Industrial CI-50",
            ["asset.code"] = "AST-0002",
            ["recipient.name"] = "Juan Pérez"
        },
        CommunicationEntityScope.Asset => new Dictionary<string, object>
        {
            ["asset.name"] = "Motor Eléctrico ME-100",
            ["asset.code"] = "AST-0003",
            ["asset.state"] = "activo",
            ["asset.toState"] = "mantenimiento",
            ["recipient.name"] = "Juan Pérez"
        },
        _ => new Dictionary<string, object>()
    };

    /// <summary>
    /// Prefija un banner visible encima del cuerpo para que quede claro
    /// que el correo es una prueba con datos ficticios.
    /// </summary>
    private static string InsertTestBanner(string body)
    {
        const string banner = "<div style=\"background:#fef3c7;border:1px solid #f59e0b;border-radius:6px;padding:10px 14px;margin-bottom:16px;font-family:Arial,sans-serif;font-size:13px;color:#92400e;\">⚠️ Correo de prueba — los datos son ficticios y este envío no corresponde a un evento real.</div>";
        return banner + body;
    }

    private static bool IsValidEmail(string email)
    {
        // Validación simple y suficiente para el caso de uso
        return email.Contains('@') && email.IndexOf('@') > 0 && email.IndexOf('@') < email.Length - 1 && !email.Contains(' ');
    }
}
