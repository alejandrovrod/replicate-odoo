using Erp.Domain.Entities;

namespace Erp.Application.DTOs;

/// <summary>Custom field definition payload (extensibility-engine Fase 1 catalog).</summary>
public sealed record CustomFieldDefinitionDto(
    Guid Id,
    string EntityName,
    string FieldName,
    string FieldType,
    bool IsRequired,
    string? DefaultValue,
    string? ValidationJsonSchema,
    bool IsActive)
{
    public static CustomFieldDefinitionDto From(CustomFieldDefinition definition) =>
        new(
            definition.Id,
            definition.EntityName,
            definition.FieldName,
            definition.FieldType,
            definition.IsRequired,
            definition.DefaultValue,
            definition.ValidationJsonSchema,
            definition.IsActive);
}
