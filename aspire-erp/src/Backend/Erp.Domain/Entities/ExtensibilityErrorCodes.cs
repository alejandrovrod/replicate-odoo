namespace Erp.Domain.Entities;

/// <summary>
/// Stable machine-readable failure codes for the extensibility engine (Fase 1). They flow
/// Domain -&gt; Application (<c>Error.Code</c>) -&gt; Api, where controllers map them to RFC 7807
/// ProblemDetails - mirroring <see cref="SellingErrorCodes"/>.
/// </summary>
public static class ExtensibilityErrorCodes
{
    /// <summary>CustomData violates the tenant's active CustomFieldDefinition catalog.</summary>
    public const string CustomFieldValidationFailed = "custom_field_validation_failed";

    /// <summary>A lifecycle hook aborted the operation (generic fallback; hooks SHOULD throw
    /// their own stable code, e.g. "insurance_authorization_required", which travels verbatim).
    /// </summary>
    public const string LifecycleHookRejected = "lifecycle_hook_rejected";

    public const string CustomFieldDefinitionNotFound = "custom_field_definition_not_found";

    public const string DuplicateCustomField = "duplicate_custom_field";

    public const string InvalidFieldType = "invalid_field_type";
}
