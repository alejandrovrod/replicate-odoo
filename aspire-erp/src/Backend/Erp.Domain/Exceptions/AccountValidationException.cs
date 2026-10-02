namespace Erp.Domain.Exceptions;

/// <summary>
/// Raised when an Account invariant is violated (DDD: invariants throw). Carries the stable
/// <see cref="Code"/> from <c>Erp.Domain.Entities.AccountErrorCodes</c> so upper layers can map the
/// failure to an RFC 7807 response without string-matching messages.
/// </summary>
public sealed class AccountValidationException : Exception
{
    public string Code { get; }

    public AccountValidationException(string code, string message) : base(message)
    {
        Code = code;
    }
}
