using System.Globalization;
using System.Reflection;
using Erp.Api.Shared;
using Erp.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Xunit;

namespace Erp.Api.IntegrationTests;

/// <summary>
/// Spec 00-i18n principle "one source of truth per layer": every public error-code constant in
/// <c>Erp.Domain</c> must be resolvable in the <c>ErrorMessages</c> catalog for both the neutral
/// (English) resource and the Spanish satellite. A code missing from either file silently ships
/// English text to a Spanish caller - the exact failure the catalog exists to prevent.
/// </summary>
/// <remarks>
/// Deliberately file-free: the localizer is resolved from the running host, so the assertion
/// exercises the real resx manifest names produced by <c>ResourcesPath</c> + marker-type
/// namespace rather than a duplicated path string. No HTTP request is issued.
/// </remarks>
public class ErrorCatalogCoverageTests : IClassFixture<ErpApiFactory>
{
    private readonly ErpApiFactory _factory;

    public ErrorCatalogCoverageTests(ErpApiFactory factory) => _factory = factory;

    /// <summary>Every <c>*ErrorCodes</c> constant resolves in the culture-less English resource.</summary>
    [Fact]
    public void AllPublicErrorCodeConstants_ResolveInNeutralResource()
        => AssertEveryCodeResolves(CultureInfo.InvariantCulture);

    /// <summary>The same codes resolve in Spanish - no silent fallback to English.</summary>
    [Fact]
    public void AllPublicErrorCodeConstants_ResolveInSpanishResource()
        => AssertEveryCodeResolves(new CultureInfo("es"));

    private void AssertEveryCodeResolves(CultureInfo culture)
    {
        var localizer = _factory.Services.GetRequiredService<IStringLocalizer<ErrorMessages>>();
        var codes = CollectErrorCodeConstants();

        Assert.NotEmpty(codes);

        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = culture;
        try
        {
            var missing = codes
                .Where(code => localizer[code].ResourceNotFound)
                .OrderBy(code => code, StringComparer.Ordinal)
                .ToList();

            Assert.True(
                missing.Count == 0,
                $"{missing.Count} domain error code(s) missing from ErrorMessages for " +
                $"'{culture.Name}': {string.Join(", ", missing)}");
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    /// <summary>
    /// Reads every <c>public const string</c> declared on a public <c>*ErrorCodes</c> static class
    /// in <c>Erp.Domain</c>. Reflection, not source parsing, so the guard cannot drift from the
    /// assembly that actually runs.
    /// </summary>
    private static IReadOnlyList<string> CollectErrorCodeConstants()
    {
        var codes = new List<string>();

        foreach (var type in typeof(CRMErrorCodes).Assembly.GetExportedTypes())
        {
            // Error catalogs are static classes, so IsAbstract && IsSealed; the name check keeps
            // the sweep narrow if a future type happens to expose snake_case constants.
            if (!type.IsAbstract || !type.IsSealed
                || !type.Name.EndsWith("ErrorCodes", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field is { IsLiteral: true, FieldType: var fieldType }
                    && fieldType == typeof(string)
                    && field.GetRawConstantValue() is string value
                    && value.Contains('_'))
                {
                    codes.Add(value);
                }
            }
        }

        return codes.Distinct(StringComparer.Ordinal).ToList();
    }
}
