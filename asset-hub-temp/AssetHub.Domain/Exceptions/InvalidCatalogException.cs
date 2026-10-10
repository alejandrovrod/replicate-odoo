namespace AssetHub.Domain.Exceptions;

/// <summary>
/// Payload references catalog ids that are invalid or belong to another tenant.
/// Mapped to HTTP 400 with an RFC 7807 body by ExceptionHandlingMiddleware.
/// </summary>
public class InvalidCatalogException : DomainException
{
    public InvalidCatalogException()
        : base(
            "invalid_catalog",
            "One or more DefaultCatalogIds are invalid or do not belong to this tenant.",
            "Domain.InvalidCatalogIds")
    {
    }
}
