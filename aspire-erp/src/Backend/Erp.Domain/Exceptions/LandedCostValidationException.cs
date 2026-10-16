namespace Erp.Domain.Exceptions;

/// <summary>
/// Raised when a landed cost voucher invariant is violated (Fase 4). Carries the stable
/// <see cref="Code"/> from <see cref="Entities.LandedCostErrorCodes"/> so upper layers map
/// the failure to an RFC 7807 response without string-matching messages - the exact shape
/// of <see cref="StockValidationException"/>.
/// </summary>
public sealed class LandedCostValidationException : Exception
{
    public string Code { get; }

    public LandedCostValidationException(string code, string message) : base(message)
    {
        Code = code;
    }
}
