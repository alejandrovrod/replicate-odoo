namespace Erp.Domain.Exceptions;

/// <summary>
/// Raised when a Selling module invariant is violated (DDD: invariants throw). Carries the stable
/// <see cref="Code"/> from <c>Erp.Domain.Entities.SellingErrorCodes</c> so upper layers can map
/// the failure to an RFC 7807 response without string-matching messages - the exact shape of
/// <see cref="PurchaseValidationException"/> and <see cref="AccountValidationException"/>.
/// </summary>
public sealed class CustomerValidationException : Exception
{
    public string Code { get; }

    public CustomerValidationException(string code, string message) : base(message)
    {
        Code = code;
    }
}
