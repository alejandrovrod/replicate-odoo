namespace Erp.Domain.Exceptions;

/// <summary>
/// Raised when a Manufacturing invariant is violated (DDD: invariants throw). Carries the stable
/// <see cref="Code"/> from <c>Erp.Domain.Entities.ManufacturingErrorCodes</c> so upper layers can
/// map the failure to an RFC 7807 response without string-matching messages. Mirrors
/// <see cref="StockValidationException"/>.
/// </summary>
public sealed class ManufacturingValidationException : Exception
{
    public string Code { get; }

    public ManufacturingValidationException(string code, string message) : base(message)
    {
        Code = code;
    }
}
