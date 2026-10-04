namespace Erp.Domain.Exceptions;

/// <summary>
/// Raised when an Asset Management invariant is violated (DDD: invariants throw). Carries the
/// stable <see cref="Code"/> from <c>Erp.Domain.Entities.AssetErrorCodes</c> so upper layers can
/// map the failure to an RFC 7807 response without string-matching messages. Mirrors
/// <see cref="StockValidationException"/> and <see cref="ManufacturingValidationException"/>.
/// </summary>
public sealed class AssetValidationException : Exception
{
    public string Code { get; }

    public AssetValidationException(string code, string message) : base(message)
    {
        Code = code;
    }
}
