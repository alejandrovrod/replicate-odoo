namespace AssetHub.Domain.Exceptions;

/// <summary>
/// The asset template is referenced by existing assets, so it cannot be deleted.
/// Mapped to HTTP 409 with an RFC 7807 body by ExceptionHandlingMiddleware.
/// </summary>
public class AssetTemplateInUseException : DomainException
{
    public AssetTemplateInUseException(int usages)
        : base(
            "asset_template_in_use",
            $"The template is being used by {usages} assets.",
            "Domain.AssetTemplateInUse",
            usages)
    {
    }
}
