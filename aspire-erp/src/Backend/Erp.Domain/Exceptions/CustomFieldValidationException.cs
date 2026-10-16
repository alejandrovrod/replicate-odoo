namespace Erp.Domain.Exceptions;

/// <summary>
/// Raised when <c>CustomData</c> violates the tenant's active <c>CustomFieldDefinition</c>
/// catalog (extensibility-engine Fase 1, gap D2). Carries the stable <c>Code</c>
/// (<see cref="Entities.ExtensibilityErrorCodes.CustomFieldValidationFailed"/>) so upper
/// layers map it to RFC 7807 without string-matching messages - the exact shape of
/// <see cref="CustomerValidationException"/>.
/// </summary>
public sealed class CustomFieldValidationException : Exception
{
    public string Code { get; }

    public CustomFieldValidationException(string code, string message) : base(message)
    {
        Code = code;
    }
}
