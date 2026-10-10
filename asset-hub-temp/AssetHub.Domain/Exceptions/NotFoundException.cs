namespace AssetHub.Domain.Exceptions;

/// <summary>
/// The requested resource does not exist (or is not visible to the caller).
/// Mapped to HTTP 404 with an RFC 7807 body by ExceptionHandlingMiddleware.
/// </summary>
public class NotFoundException : DomainException
{
    public NotFoundException() : base("not_found", "The requested resource was not found.")
    {
    }

    public NotFoundException(string resource, object key)
        : base("not_found", $"Resource '{resource}' with id '{key}' was not found.", "Error_NotFoundResource", resource, key)
    {
    }

    public NotFoundException(string code, string message, string resourceKey, params object[] args)
        : base(code, message, resourceKey, args)
    {
    }
}
