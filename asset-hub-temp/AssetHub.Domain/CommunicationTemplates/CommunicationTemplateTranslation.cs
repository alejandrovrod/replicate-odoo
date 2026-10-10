using System;

namespace AssetHub.Domain.CommunicationTemplates;

public class CommunicationTemplateTranslation
{
    public Guid Id { get; set; }
    public Guid VersionId { get; set; }
    public CommunicationTemplateVersion? Version { get; set; }

    // Codigo de idioma ISO, ej. "es", "en"
    public string Locale { get; set; } = string.Empty;

    // Solo aplica para plantillas de Email (null en Documents)
    public string? Subject { get; set; }

    // Cuerpo HTML de la plantilla
    public string Content { get; set; } = string.Empty;

    // JSON de diseño generado por react-email-editor (Unlayer) para permitir volver a editar visualmente
    public string? DesignJson { get; set; }
}
