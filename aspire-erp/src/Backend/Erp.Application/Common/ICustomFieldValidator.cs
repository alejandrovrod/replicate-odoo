namespace Erp.Application.Common;

/// <summary>
/// Validates a <c>CustomData</c> JSON payload against the tenant's active
/// <c>CustomFieldDefinition</c> catalog (extensibility-engine Fase 1, gap D2). In-memory
/// validation (System.Text.Json only, no external schema package): the catalog IS the schema.
/// </summary>
/// <remarks>
/// Returns <c>null</c> when the payload is valid; otherwise the RFC 7807-ready
/// <see cref="Error"/> (code <c>custom_field_validation_failed</c>). An empty/whitespace
/// payload is valid only when the entity carries NO required definitions.
/// </remarks>
public interface ICustomFieldValidator
{
    /// <param name="entityName">Root entity name, e.g. "Customer" (matches the catalog).</param>
    /// <param name="customData">Raw JSON object string from the command; null/empty when none.</param>
    Task<Error?> ValidateAsync(
        string entityName, string? customData, CancellationToken cancellationToken = default);
}
