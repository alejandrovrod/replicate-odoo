using System.Text.Json;
using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Extensibility.Commands;

/// <summary>Registers one custom field for a root entity (extensibility-engine Fase 1 catalog).</summary>
public sealed record CreateCustomFieldDefinitionCommand(
    string EntityName,
    string FieldName,
    string FieldType,
    bool IsRequired = false,
    string? DefaultValue = null,
    string? ValidationJsonSchema = null) : ICommand<Result<CustomFieldDefinitionDto>>;

/// <summary>Updates one custom field definition (type/entity/name are immutable after creation).</summary>
public sealed record UpdateCustomFieldDefinitionCommand(
    Guid Id,
    bool IsRequired,
    string? DefaultValue,
    string? ValidationJsonSchema,
    bool IsActive) : ICommand<Result<CustomFieldDefinitionDto>>;

/// <summary>Hard-deletes one catalog row (definitions are config, never ledger data).</summary>
public sealed record DeleteCustomFieldDefinitionCommand(Guid Id) : ICommand<Result<bool>>;

internal static class CustomFieldDefinitionGuard
{
    internal static readonly string[] AllowedTypes = ["string", "number", "boolean", "date", "select"];

    internal static string NormalizeType(string fieldType)
    {
        var normalized = (fieldType ?? string.Empty).Trim().ToLowerInvariant();
        if (!AllowedTypes.Contains(normalized))
        {
            throw new CustomFieldValidationException(
                ExtensibilityErrorCodes.InvalidFieldType,
                $"Field type '{fieldType}' is not supported. Allowed: {string.Join(", ", AllowedTypes)}.");
        }

        return normalized;
    }

    internal static string NormalizeName(string value, string role)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 100)
        {
            throw new CustomFieldValidationException(
                ExtensibilityErrorCodes.CustomFieldValidationFailed,
                $"{role} is required and must be at most 100 characters.");
        }

        return value.Trim();
    }

    /// <summary>
    /// The constraint blob must be a JSON object when present; a select REQUIRES an options
    /// array (otherwise the UI could never render it and validation could never pass).
    /// </summary>
    internal static string? NormalizeSchema(string? schema, string fieldType)
    {
        if (string.IsNullOrWhiteSpace(schema))
        {
            if (fieldType == "select")
            {
                throw new CustomFieldValidationException(
                    ExtensibilityErrorCodes.CustomFieldValidationFailed,
                    "A select field requires ValidationJsonSchema with an 'options' array.");
            }

            return null;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(schema);
        }
        catch (JsonException ex)
        {
            throw new CustomFieldValidationException(
                ExtensibilityErrorCodes.CustomFieldValidationFailed,
                $"ValidationJsonSchema is not valid JSON: {ex.Message}");
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new CustomFieldValidationException(
                    ExtensibilityErrorCodes.CustomFieldValidationFailed,
                    "ValidationJsonSchema must be a JSON object.");
            }

            if (fieldType == "select"
                && (!document.RootElement.TryGetProperty("options", out var options)
                    || options.ValueKind != JsonValueKind.Array
                    || options.GetArrayLength() == 0))
            {
                throw new CustomFieldValidationException(
                    ExtensibilityErrorCodes.CustomFieldValidationFailed,
                    "A select field requires ValidationJsonSchema with a non-empty 'options' array.");
            }
        }

        return schema;
    }
}

public sealed class CreateCustomFieldDefinitionCommandHandler
    : ICommandHandler<CreateCustomFieldDefinitionCommand, Result<CustomFieldDefinitionDto>>
{
    private readonly ICustomFieldDefinitionRepository _definitions;

    public CreateCustomFieldDefinitionCommandHandler(ICustomFieldDefinitionRepository definitions)
    {
        _definitions = definitions;
    }

    public async Task<Result<CustomFieldDefinitionDto>> HandleAsync(
        CreateCustomFieldDefinitionCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = CustomFieldDefinitionGuard.NormalizeName(command.EntityName, "EntityName");
            var field = CustomFieldDefinitionGuard.NormalizeName(command.FieldName, "FieldName");
            var type = CustomFieldDefinitionGuard.NormalizeType(command.FieldType);
            var schema = CustomFieldDefinitionGuard.NormalizeSchema(command.ValidationJsonSchema, type);

            if (await _definitions.ExistsAsync(entity, field, null, cancellationToken))
            {
                throw new CustomFieldValidationException(
                    ExtensibilityErrorCodes.DuplicateCustomField,
                    $"Custom field '{field}' already exists for entity '{entity}'.");
            }

            var definition = new CustomFieldDefinition
            {
                Id = Guid.NewGuid(),
                EntityName = entity,
                FieldName = field,
                FieldType = type,
                IsRequired = command.IsRequired,
                DefaultValue = command.DefaultValue,
                ValidationJsonSchema = schema,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            await _definitions.AddAsync(definition, cancellationToken);
            return Result<CustomFieldDefinitionDto>.Success(CustomFieldDefinitionDto.From(definition));
        }
        catch (CustomFieldValidationException ex)
        {
            return Result<CustomFieldDefinitionDto>.Failure(ex.Code, ex.Message);
        }
    }
}

public sealed class UpdateCustomFieldDefinitionCommandHandler
    : ICommandHandler<UpdateCustomFieldDefinitionCommand, Result<CustomFieldDefinitionDto>>
{
    private readonly ICustomFieldDefinitionRepository _definitions;

    public UpdateCustomFieldDefinitionCommandHandler(ICustomFieldDefinitionRepository definitions)
    {
        _definitions = definitions;
    }

    public async Task<Result<CustomFieldDefinitionDto>> HandleAsync(
        UpdateCustomFieldDefinitionCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            var definition = await _definitions.GetByIdAsync(command.Id, cancellationToken)
                ?? throw new CustomFieldValidationException(
                    ExtensibilityErrorCodes.CustomFieldDefinitionNotFound,
                    $"Custom field definition '{command.Id}' was not found in this tenant.");

            definition.IsRequired = command.IsRequired;
            definition.DefaultValue = command.DefaultValue;
            definition.ValidationJsonSchema = CustomFieldDefinitionGuard.NormalizeSchema(
                command.ValidationJsonSchema, definition.FieldType);
            definition.IsActive = command.IsActive;

            await _definitions.UpdateAsync(definition, cancellationToken);
            return Result<CustomFieldDefinitionDto>.Success(CustomFieldDefinitionDto.From(definition));
        }
        catch (CustomFieldValidationException ex)
        {
            return Result<CustomFieldDefinitionDto>.Failure(ex.Code, ex.Message);
        }
    }
}

public sealed class DeleteCustomFieldDefinitionCommandHandler
    : ICommandHandler<DeleteCustomFieldDefinitionCommand, Result<bool>>
{
    private readonly ICustomFieldDefinitionRepository _definitions;

    public DeleteCustomFieldDefinitionCommandHandler(ICustomFieldDefinitionRepository definitions)
    {
        _definitions = definitions;
    }

    public async Task<Result<bool>> HandleAsync(
        DeleteCustomFieldDefinitionCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            var definition = await _definitions.GetByIdAsync(command.Id, cancellationToken)
                ?? throw new CustomFieldValidationException(
                    ExtensibilityErrorCodes.CustomFieldDefinitionNotFound,
                    $"Custom field definition '{command.Id}' was not found in this tenant.");

            await _definitions.DeleteAsync(definition, cancellationToken);
            return Result<bool>.Success(true);
        }
        catch (CustomFieldValidationException ex)
        {
            return Result<bool>.Failure(ex.Code, ex.Message);
        }
    }
}
