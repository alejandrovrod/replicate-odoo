namespace Erp.Domain.Exceptions;

/// <summary>
/// Raised when an HR &amp; Payroll invariant is violated (DDD: invariants throw). Carries the
/// stable <see cref="Code"/> from <c>Erp.Domain.Entities.HrPayrollErrorCodes</c> so upper layers
/// can map the failure to an RFC 7807 response without string-matching messages. Mirrors
/// <see cref="StockValidationException"/> and <see cref="AssetValidationException"/>.
/// </summary>
public sealed class HrValidationException : Exception
{
    public string Code { get; }

    public HrValidationException(string code, string message) : base(message)
    {
        Code = code;
    }
}
