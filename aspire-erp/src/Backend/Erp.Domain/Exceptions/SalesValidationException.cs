namespace Erp.Domain.Exceptions;

/// <summary>
/// Raised when a Selling document invariant is violated (DDD: invariants throw). Carries the
/// stable <see cref="Code"/> from <c>Erp.Domain.Entities.SellingErrorCodes</c> so upper layers can
/// map the failure to an RFC 7807 response without string-matching messages - the exact shape of
/// <see cref="PurchaseValidationException"/> and <see cref="CustomerValidationException"/>.
/// </summary>
/// <remarks>
/// Deliberately named for the DOCUMENT family (SalesOrder/DeliveryNote) rather than reusing
/// <see cref="CustomerValidationException"/>, which reads as a customer-master failure while
/// carrying a sales-order code (tasks 5.2/5.2b throw this from the order and delivery flows).
/// </remarks>
public sealed class SalesValidationException : Exception
{
    public string Code { get; }

    public SalesValidationException(string code, string message) : base(message)
    {
        Code = code;
    }
}
