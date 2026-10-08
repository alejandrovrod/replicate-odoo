namespace Erp.Domain.Exceptions;

/// <summary>
/// Raised when a Currency invariant is violated (DDD: invariants throw). Carries the stable
/// <see cref="Code"/> from <c>Erp.Domain.Entities.CurrencyErrorCodes</c> so upper layers can map the
/// failure to an RFC 7807 response without string-matching messages.
/// </summary>
public sealed class CurrencyValidationException : Exception
{
    public string Code { get; }

    public CurrencyValidationException(string code, string message) : base(message)
    {
        Code = code;
    }
}
