using Microsoft.Extensions.Localization;

namespace Erp.Api.Localization;

/// <summary>
/// Safe key lookup for localized ProblemDetails (spec 00-i18n, fallback rule: a key missing in
/// the requested culture falls back to the neutral (English) resx; a key missing everywhere
/// falls back to the caller-supplied message instead of leaking the raw key to API consumers.
/// </summary>
public static class LocalizerExtensions
{
    public static string Text(this IStringLocalizer localizer, string key, string? fallback = null)
    {
        var localized = localizer[key];
        return localized.ResourceNotFound ? fallback ?? key : localized.Value;
    }
}
