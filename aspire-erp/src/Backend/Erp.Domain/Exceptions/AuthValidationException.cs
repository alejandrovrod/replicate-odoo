namespace Erp.Domain.Exceptions;

/// <summary>
/// Raised when a User Profile &amp; Security invariant is violated (DDD: invariants throw).
/// Carries the stable <see cref="Code"/> from <c>AuthErrorCodes</c> so upper layers can map
/// the failure to an RFC 7807 response without string-matching messages.
/// </summary>
public sealed class AuthValidationException : Exception
{
    public string Code { get; }

    public AuthValidationException(string code, string message) : base(message)
    {
        Code = code;
    }
}
