namespace Erp.Domain.Exceptions;

/// <summary>
/// Raised when a Stock &amp; Inventory invariant is violated (DDD: invariants throw). Carries the
/// stable <see cref="Code"/> from <c>Erp.Domain.Entities.StockErrorCodes</c> so upper layers can map
/// the failure to an RFC 7807 response without string-matching messages.
/// </summary>
public sealed class StockValidationException : Exception
{
    public string Code { get; }

    public StockValidationException(string code, string message) : base(message)
    {
        Code = code;
    }
}
