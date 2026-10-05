using System;

namespace Erp.Domain.Exceptions;

/// <summary>
/// Thrown when a CRM business rule is violated (e.g. Lead conversion guards, Opportunity probability bounds).
/// Generates an RFC 7807 400 Bad Request.
/// </summary>
public sealed class CRMValidationException : Exception
{
    public string Code { get; }

    public CRMValidationException(string code, string message) : base(message)
    {
        Code = code;
    }
}
