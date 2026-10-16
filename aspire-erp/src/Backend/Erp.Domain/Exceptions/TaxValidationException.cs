using Erp.Domain.Entities;

namespace Erp.Domain.Exceptions;

/// <summary>
/// Raised when a tax-engine invariant is violated (tax-engine Fase 2): unknown charge type
/// or basis, out-of-range rate, or a misconfigured template row. Carries the stable
/// <see cref="Code"/> from <see cref="TaxErrorCodes"/> so upper layers map the failure to
/// an RFC 7807 response without string-matching messages - the exact shape of
/// <see cref="CustomerValidationException"/>.
/// </summary>
public sealed class TaxValidationException : Exception
{
    public string Code { get; }

    public TaxValidationException(string code, string message) : base(message)
    {
        Code = code;
    }
}
