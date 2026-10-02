namespace Erp.Domain.Exceptions;

/// <summary>
/// Raised when a Buying module invariant is violated (DDD: invariants throw). Carries the stable
/// <see cref="Code"/> from <c>Erp.Domain.Entities.PurchaseErrorCodes</c> so upper layers can map
/// the failure to an RFC 7807 response without string-matching messages.
/// </summary>
public sealed class PurchaseValidationException : Exception
{
    public string Code { get; }

    public PurchaseValidationException(string code, string message) : base(message)
    {
        Code = code;
    }
}
