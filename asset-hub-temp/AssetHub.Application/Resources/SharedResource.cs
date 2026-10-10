namespace AssetHub.Application.Resources;

/// <summary>
/// Marker type for the message catalog shared by the whole platform.
///
/// The .resx files sit next to this class (<c>SharedResource.resx</c> neutral,
/// <c>SharedResource.es.resx</c>, <c>SharedResource.en.resx</c>) so the embedded
/// resource names match this type's full name exactly and no ResourcesPath
/// hint is needed. Register with <c>builder.Services.AddLocalization()</c>.
///
/// Used via <c>IStringLocalizer&lt;SharedResource&gt;</c> to resolve every string
/// that reaches the client (ProblemDetails, domain errors, validation messages)
/// against the culture of the current request.
/// </summary>
public sealed class SharedResource
{
    private SharedResource()
    {
    }
}
