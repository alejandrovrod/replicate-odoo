namespace AssetHub.Domain.Exceptions;

/// <summary>
/// The operation conflicts with the current state of the resource
/// (duplicate, concurrent update, already-executed transition).
/// Mapped to HTTP 409 with an RFC 7807 body by ExceptionHandlingMiddleware.
/// </summary>
public class ConflictException : DomainException
{
    public ConflictException() : base("conflict", "The operation conflicts with the current state of the resource.")
    {
    }

    public ConflictException(string code, string message)
        : base(code, message)
    {
    }

    public ConflictException(string code, string message, string resourceKey, params object[] args)
        : base(code, message, resourceKey, args)
    {
    }
}
