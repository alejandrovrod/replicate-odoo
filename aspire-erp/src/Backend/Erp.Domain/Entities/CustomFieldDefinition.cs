using Erp.Domain.Common;

namespace Erp.Domain.Entities;

/// <summary>
/// Tenant-scoped catalog of one dynamic attribute (extensibility-engine Fase 1, gap D1):
/// which field (<see cref="FieldName"/>) of which root entity (<see cref="EntityName"/>) may
/// appear inside <see cref="ICustomFieldsCarrier.CustomData"/>, of which shape
/// (<see cref="FieldType"/>) and whether it is mandatory (<see cref="IsRequired"/>).
/// </summary>
/// <remarks>
/// <para>There is NO <c>AggregateRoot&lt;T&gt;</c> base in this codebase (all aggregates implement
/// <see cref="ITenantEntity"/> directly), so this entity follows the same shape: plain class +
/// <see cref="ITenantEntity"/>. Tenant-wide by design (no CompanyId): one definition governs
/// every company of the tenant, and (EntityName, FieldName) is unique per tenant.</para>
/// <para><see cref="FieldType"/> vocabulary: <c>string</c>, <c>number</c>, <c>boolean</c>,
/// <c>date</c> (ISO yyyy-MM-dd), <c>select</c> (value must be one of the options declared in
/// <see cref="ValidationJsonSchema"/>). <see cref="ValidationJsonSchema"/> optionally carries
/// extra constraints as a JSON object (<c>{"options":[...]}</c> for select,
/// <c>{"minLength":n}</c>, <c>{"pattern":"..."}</c>, <c>{"minimum":n,"maximum":n}</c>).
/// <see cref="DefaultValue"/> is the raw string applied by the UI when the user leaves the
/// field empty (never auto-applied server-side: an absent optional field simply stays absent).
/// </para>
/// </remarks>
public class CustomFieldDefinition : ITenantEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant; immutable after creation (Constitution Article II.4).</summary>
    public Guid TenantId { get; set; }

    /// <summary>Root entity name, e.g. "Customer", "Item" (matches the frontend entityType).</summary>
    public string EntityName { get; set; } = string.Empty;

    /// <summary>JSON key inside CustomData, e.g. "bloodType".</summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>One of: string, number, boolean, date, select.</summary>
    public string FieldType { get; set; } = string.Empty;

    /// <summary>When true, CustomData must carry a valid value for this field.</summary>
    public bool IsRequired { get; set; }

    /// <summary>Raw default suggested by the UI; null/empty means none.</summary>
    public string? DefaultValue { get; set; }

    /// <summary>Extra constraints as a JSON object (options/pattern/min/max); null means none.</summary>
    public string? ValidationJsonSchema { get; set; }

    /// <summary>Inactive definitions are ignored by validation (soft-retire, never deleted).</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
}
