namespace Erp.Domain.Common;

/// <summary>
/// Marks a root aggregate that can carry tenant-configured dynamic attributes
/// (extensibility-engine Fase 1, gap D1): vertical data (e.g. a policy number on Customer,
/// a VIN on Asset) travels as a JSON object string in <see cref="CustomData"/> instead of
/// schema columns per industry. The Core never branches on its content (zero-regression rule:
/// no <c>if (industry == ...)</c> in the nucleus) - it is validated against the tenant's
/// <c>CustomFieldDefinition</c> catalog before persistence and read back verbatim.
/// </summary>
/// <remarks>
/// Nullable string (not a JSON column type): SQL Server stores it as <c>NVARCHAR(MAX)</c>,
/// NULL when the document carries no custom attributes. Validation lives in
/// <c>Erp.Application</c> (<c>ICustomFieldValidator</c>), never in the aggregate.
/// </remarks>
public interface ICustomFieldsCarrier
{
    string? CustomData { get; set; }
}
