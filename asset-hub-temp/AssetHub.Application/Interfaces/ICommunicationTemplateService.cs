using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Domain.CommunicationTemplates;

namespace AssetHub.Application.Interfaces;

/// <summary>
/// Resuelve la plantilla activa de un tenant para un scope/tipo dado y renderiza
/// asunto y cuerpo en el idioma del destinatario (con fallback a "es").
/// Si no hay plantilla definida por el tenant, devuelve null para que el caller
/// use su fallback hardcodeado.
/// </summary>
public interface ICommunicationTemplateService
{
    Task<RenderedCommunication?> RenderActiveAsync(
        Guid tenantId,
        string templateCode,
        string recipientLocale,
        IReadOnlyDictionary<string, object> variables,
        CancellationToken cancellationToken = default);

    Task<RenderedCommunication?> RenderByIdAsync(
        Guid tenantId,
        Guid templateId,
        string recipientLocale,
        IReadOnlyDictionary<string, object> variables,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Renderiza un set de traducciones arbitrario (ej. contenido no guardado
    /// que el usuario esta editando) en el idioma indicado (fallback "es").
    /// </summary>
    Task<RenderedCommunication> RenderTranslationsAsync(
        Guid tenantId,
        System.Collections.Generic.IEnumerable<CommunicationTemplateTranslation> translations,
        string locale,
        IReadOnlyDictionary<string, object> variables,
        CancellationToken cancellationToken = default);
}

public record RenderedCommunication(string? Subject, string Body);
