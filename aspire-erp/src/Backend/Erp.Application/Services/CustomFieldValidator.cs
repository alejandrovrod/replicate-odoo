using System.Globalization;
using System.Text.Json;
using Erp.Application.Common;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Services;

/// <summary>
/// In-memory <see cref="ICustomFieldValidator"/> (extensibility-engine Fase 1, gap D2):
/// the tenant's active <see cref="CustomFieldDefinition"/> rows ARE the schema, evaluated
/// with System.Text.Json only (no external schema package - Erp.Application carries zero
/// PackageReferences by design).
/// </summary>
/// <remarks>
/// <para>Supported <c>FieldType</c> vocabulary: <c>string</c> (JsonValueKind.String),
/// <c>number</c> (JsonValueKind.Number), <c>boolean</c> (True/False), <c>date</c> (string in
/// ISO <c>yyyy-MM-dd</c>), <c>select</c> (string within the <c>options</c> array declared in
/// <c>ValidationJsonSchema</c>).</para>
/// <para>Extra constraints (all optional, read from <c>ValidationJsonSchema</c> as a JSON
/// object): <c>minLength</c>/<c>maxLength</c> (string), <c>pattern</c> (string, .NET regex),
/// <c>minimum</c>/<c>maximum</c> (number), <c>options</c> (select; REQUIRED for select -
/// a select without options is a catalog misconfiguration and fails validation).</para>
/// <para>Unknown keys in the payload are IGNORED (forward compatibility: a retired definition
/// never breaks stored documents). A non-object JSON payload, or malformed JSON, fails with
/// <c>custom_field_validation_failed</c>.</para>
/// </remarks>
public sealed class CustomFieldValidator : ICustomFieldValidator
{
    private readonly ICustomFieldDefinitionRepository _definitions;

    public CustomFieldValidator(ICustomFieldDefinitionRepository definitions)
    {
        _definitions = definitions;
    }

    public async Task<Error?> ValidateAsync(
        string entityName, string? customData, CancellationToken cancellationToken = default)
    {
        var definitions = (await _definitions.GetActiveByEntityAsync(entityName, cancellationToken))
            .Where(d => d.IsActive)
            .ToList();

        if (string.IsNullOrWhiteSpace(customData))
        {
            var missing = definitions.Where(d => d.IsRequired).Select(d => d.FieldName).ToList();
            return missing.Count == 0
                ? null
                : Fail($"Missing required custom field(s): {string.Join(", ", missing)}.");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(customData);
        }
        catch (JsonException ex)
        {
            return Fail($"CustomData is not valid JSON: {ex.Message}");
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Fail("CustomData must be a JSON object.");
            }

            foreach (var definition in definitions)
            {
                if (!document.RootElement.TryGetProperty(definition.FieldName, out var value)
                    || value.ValueKind == JsonValueKind.Null
                    || value.ValueKind == JsonValueKind.Undefined)
                {
                    if (definition.IsRequired)
                    {
                        return Fail($"Missing required custom field '{definition.FieldName}'.");
                    }

                    continue;
                }

                var error = ValidateValue(definition, value);
                if (error is not null)
                {
                    return error;
                }
            }
        }

        return null;
    }

    private static Error? ValidateValue(CustomFieldDefinition definition, JsonElement value)
    {
        var type = (definition.FieldType ?? string.Empty).Trim().ToLowerInvariant();
        return type switch
        {
            "string" => ValidateString(definition, value),
            "number" => ValidateNumber(definition, value),
            "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? null
                : Fail($"Custom field '{definition.FieldName}' must be a boolean."),
            "date" => ValidateDate(definition, value),
            "select" => ValidateSelect(definition, value),
            _ => Fail($"Custom field '{definition.FieldName}' has unsupported type '{definition.FieldType}'."),
        };
    }

    private static Error? ValidateString(CustomFieldDefinition definition, JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            return Fail($"Custom field '{definition.FieldName}' must be a string.");
        }

        var text = value.GetString() ?? string.Empty;
        var constraints = ReadConstraints(definition);

        if (constraints.TryGetValue("minLength", out var min)
            && min.TryGetInt32(out var minLength) && text.Length < minLength)
        {
            return Fail($"Custom field '{definition.FieldName}' must be at least {minLength} characters.");
        }

        if (constraints.TryGetValue("maxLength", out var max)
            && max.TryGetInt32(out var maxLength) && text.Length > maxLength)
        {
            return Fail($"Custom field '{definition.FieldName}' must be at most {maxLength} characters.");
        }

        if (constraints.TryGetValue("pattern", out var pattern)
            && pattern.ValueKind == JsonValueKind.String
            && !System.Text.RegularExpressions.Regex.IsMatch(text, pattern.GetString()!))
        {
            return Fail($"Custom field '{definition.FieldName}' does not match the required pattern.");
        }

        return null;
    }

    private static Error? ValidateNumber(CustomFieldDefinition definition, JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number)
        {
            return Fail($"Custom field '{definition.FieldName}' must be a number.");
        }

        var number = value.GetDecimal();
        var constraints = ReadConstraints(definition);

        if (constraints.TryGetValue("minimum", out var min)
            && min.ValueKind == JsonValueKind.Number && number < min.GetDecimal())
        {
            return Fail($"Custom field '{definition.FieldName}' must be >= {min.GetDecimal().ToString(CultureInfo.InvariantCulture)}.");
        }

        if (constraints.TryGetValue("maximum", out var max)
            && max.ValueKind == JsonValueKind.Number && number > max.GetDecimal())
        {
            return Fail($"Custom field '{definition.FieldName}' must be <= {max.GetDecimal().ToString(CultureInfo.InvariantCulture)}.");
        }

        return null;
    }

    private static Error? ValidateDate(CustomFieldDefinition definition, JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String
            || !DateOnly.TryParseExact(
                value.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out _))
        {
            return Fail($"Custom field '{definition.FieldName}' must be a date in yyyy-MM-dd format.");
        }

        return null;
    }

    private static Error? ValidateSelect(CustomFieldDefinition definition, JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            return Fail($"Custom field '{definition.FieldName}' must be one of the configured options.");
        }

        var constraints = ReadConstraints(definition);
        if (!constraints.TryGetValue("options", out var options)
            || options.ValueKind != JsonValueKind.Array)
        {
            return Fail($"Custom field '{definition.FieldName}' is misconfigured: a select requires 'options'.");
        }

        var text = value.GetString() ?? string.Empty;
        foreach (var option in options.EnumerateArray())
        {
            if (option.ValueKind == JsonValueKind.String && option.GetString() == text)
            {
                return null;
            }
        }

        return Fail($"Custom field '{definition.FieldName}' must be one of the configured options.");
    }

    private static Dictionary<string, JsonElement> ReadConstraints(CustomFieldDefinition definition)
    {
        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(definition.ValidationJsonSchema))
        {
            return result;
        }

        try
        {
            using var document = JsonDocument.Parse(definition.ValidationJsonSchema);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return result;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                result[property.Name] = property.Value.Clone();
            }
        }
        catch (JsonException)
        {
            // A malformed constraint blob is a catalog data issue, not a payload issue:
            // ignore it here (the CRUD handler rejects malformed schemas at write time).
        }

        return result;
    }

    private static Error Fail(string message) =>
        new(ExtensibilityErrorCodes.CustomFieldValidationFailed, message);

    /// <summary>
    /// Throwing twin for handlers that work in the exception style: raises
    /// <see cref="CustomFieldValidationException"/> when invalid, no-op when valid.
    /// </summary>
    public async Task EnsureValidAsync(
        string entityName, string? customData, CancellationToken cancellationToken = default)
    {
        var error = await ValidateAsync(entityName, customData, cancellationToken);
        if (error is not null)
        {
            throw new CustomFieldValidationException(error.Code, error.Message);
        }
    }
}
