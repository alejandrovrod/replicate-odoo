using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;

namespace AssetHub.Api.Configuration;

/// <summary>
/// Single source of truth for the platform's localization setup.
///
/// Culture resolution order: Accept-Language header → query string → cookie →
/// default (<c>es</c>). Everything registered here is what makes
/// <c>CultureInfo.CurrentCulture</c>/<c>CurrentUICulture</c> correct for the
/// duration of a request, which is what <c>IStringLocalizer&lt;SharedResource&gt;</c>
/// and the problem+json bodies read from.
/// </summary>
public static class LocalizationSetup
{
    public const string DefaultCulture = "es";

    /// <summary>Cultures exposed by the API, in the order they should be preferred.</summary>
    public static readonly CultureInfo[] SupportedCultures =
    {
        new("es"),
        new("en")
    };

    public static IServiceCollection AddAssetHubLocalization(this IServiceCollection services)
    {
        // No ResourcesPath: SharedResource.resx sits next to the SharedResource
        // marker class so the embedded resource name matches the type's full name.
        services.AddLocalization();
        services.Configure<RequestLocalizationOptions>(Configure);
        return services;
    }

    /// <summary>Applies the platform defaults to a <see cref="RequestLocalizationOptions"/>.</summary>
    public static void Configure(RequestLocalizationOptions options)
    {
        options.DefaultRequestCulture = new RequestCulture(DefaultCulture);
        options.SupportedCultures = SupportedCultures;
        options.SupportedUICultures = SupportedCultures;

        // Echoes the resolved culture back as Content-Language.
        options.ApplyCurrentCultureToResponseHeaders = true;

        // Explicit ordering: the SPA always sends Accept-Language.
        options.RequestCultureProviders = new List<IRequestCultureProvider>
        {
            new AcceptLanguageHeaderRequestCultureProvider(),
            new QueryStringRequestCultureProvider(),
            new CookieRequestCultureProvider()
        };
    }
}
