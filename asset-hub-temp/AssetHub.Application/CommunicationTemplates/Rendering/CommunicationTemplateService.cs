using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.CommunicationTemplates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AssetHub.Application.CommunicationTemplates.Rendering;

/// <summary>
/// Resuelve la version activa de una plantilla por Code (aislada por tenant),
/// elige la traduccion del idioma del destinatario (fallback: "es") y renderiza
/// asunto y cuerpo con Scriban. Variables inexistentes => string vacio, sin error.
/// </summary>
public class CommunicationTemplateService : ICommunicationTemplateService
{
    private const string DefaultLocale = "es";

    private readonly ITenantDbContext _db;
    private readonly IPlatformDbContext _platformDb;
    private readonly ITemplateRenderEngine _renderEngine;
    private readonly IConfiguration _configuration;

    public CommunicationTemplateService(
        ITenantDbContext db,
        IPlatformDbContext platformDb,
        ITemplateRenderEngine renderEngine,
        IConfiguration configuration)
    {
        _db = db;
        _platformDb = platformDb;
        _renderEngine = renderEngine;
        _configuration = configuration;
    }

    public async Task<RenderedCommunication?> RenderActiveAsync(
        Guid tenantId,
        string templateCode,
        string recipientLocale,
        IReadOnlyDictionary<string, object> variables,
        CancellationToken cancellationToken = default)
    {
        var template = await _db.CommunicationTemplates
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.Code == templateCode)
            .FirstOrDefaultAsync(cancellationToken);

        if (template == null || template.ActiveVersionId == null)
        {
            return null; // Sin plantilla definida => el caller usa su fallback hardcodeado
        }

        var version = await LoadVersionAsync(tenantId, template.ActiveVersionId.Value, cancellationToken);
        if (version == null || version.Translations.Count == 0)
        {
            return null;
        }

        return await RenderTranslationsAsync(
            tenantId, version.Translations, recipientLocale, variables, cancellationToken);
    }

    public async Task<RenderedCommunication?> RenderByIdAsync(
        Guid tenantId,
        Guid templateId,
        string recipientLocale,
        IReadOnlyDictionary<string, object> variables,
        CancellationToken cancellationToken = default)
    {
        var template = await _db.CommunicationTemplates
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.Id == templateId)
            .FirstOrDefaultAsync(cancellationToken);

        if (template == null || template.ActiveVersionId == null)
        {
            return null;
        }

        var version = await LoadVersionAsync(tenantId, template.ActiveVersionId.Value, cancellationToken);
        if (version == null || version.Translations.Count == 0)
        {
            return null;
        }

        return await RenderTranslationsAsync(
            tenantId, version.Translations, recipientLocale, variables, cancellationToken);
    }

    public async Task<RenderedCommunication> RenderTranslationsAsync(
        Guid tenantId,
        IEnumerable<CommunicationTemplateTranslation> translations,
        string locale,
        IReadOnlyDictionary<string, object> variables,
        CancellationToken cancellationToken = default)
    {
        var translationList = translations?.ToList() ?? new List<CommunicationTemplateTranslation>();
        if (translationList.Count == 0)
            throw new ArgumentException("No hay traducciones para renderizar.");

        var resolvedLocale = string.IsNullOrWhiteSpace(locale) ? DefaultLocale : locale;
        var translation = translationList.FirstOrDefault(t => t.Locale == resolvedLocale)
            ?? translationList.FirstOrDefault(t => t.Locale == DefaultLocale)
            ?? translationList.First();

        var mergedVariables = await MergeTenantVariablesAsync(tenantId, variables, cancellationToken);

        var subject = translation.Subject != null
            ? _renderEngine.Render(translation.Subject, mergedVariables)
            : null;
        var body = _renderEngine.Render(translation.Content, mergedVariables);

        return new RenderedCommunication(subject, body);
    }

    private async Task<CommunicationTemplateVersion?> LoadVersionAsync(
        Guid tenantId, Guid versionId, CancellationToken cancellationToken)
    {
        return await _db.CommunicationTemplateVersions
            .AsNoTracking()
            .Where(v => v.Id == versionId)
            .Include(v => v.Translations)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Agrega tenant.name / tenant.logo_url / tenant.support_email sobre las
    /// variables provistas (datos reales del tenant o placeholders).
    /// </summary>
    private async Task<IReadOnlyDictionary<string, object>> MergeTenantVariablesAsync(
        Guid tenantId, IReadOnlyDictionary<string, object> variables, CancellationToken cancellationToken)
    {
        var merged = new Dictionary<string, object>(variables);

        var tenant = await _platformDb.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

        if (tenant != null)
        {
            var logoUrl = tenant.LogoUrl;
            if (string.IsNullOrWhiteSpace(logoUrl))
            {
                logoUrl = "https://placehold.co/400x100?text=Logo+Tenant";
            }
            else if (!logoUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                     !logoUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                var baseUrl = _configuration["App:BaseUrl"] ?? "https://localhost:7184";
                baseUrl = baseUrl.TrimEnd('/');
                logoUrl = baseUrl + (logoUrl.StartsWith("/") ? "" : "/") + logoUrl;
            }

            merged["tenant"] = new
            {
                name = tenant.Name,
                logo_url = logoUrl,
                support_email = tenant.SupportEmail ?? "soporte@sonnora.mx"
            };
        }

        return merged;
    }
}
