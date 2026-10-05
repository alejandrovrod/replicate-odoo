namespace Erp.Api.Shared;

/// <summary>
/// Marker class for <c>IStringLocalizer&lt;ErrorMessages&gt;</c>. The namespace must stay
/// <c>Erp.Api.Shared</c> so the resource prefix (RootNamespace + ResourcesPath + type name)
/// resolves to <c>Erp.Api.Resources.Shared.ErrorMessages</c>, matching the manifest name of
/// Resources/Shared/ErrorMessages.resx.
/// </summary>
/// <remarks>
/// The neutral (culture-less) resx holds the English text: the .NET resource fallback chain for
/// culture "es" goes es -&gt; neutral, NEVER es -&gt; "en" (satellites of a sibling culture are not
/// consulted). Putting English in ErrorMessages.en.resx would break the "missing key in es falls
/// back to en" rule; the neutral file IS the en fallback.
/// </remarks>
public sealed class ErrorMessages { }
