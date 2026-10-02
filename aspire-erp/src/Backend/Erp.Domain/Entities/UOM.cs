using Erp.Domain.Common;

namespace Erp.Domain.Entities;

/// <summary>
/// Unit of Measure of one tenant (ubiquitous language: "UOM" - .specify/spec.md §4 Module 3).
/// Conversion factors are expressed against the consuming Item's Base UOM.
/// </summary>
/// <remarks>
/// Tenant-scoped: implements <c>ITenantEntity</c> (Constitution Article II.1). Unique per tenant
/// through the <c>(TenantId, Code)</c> index (Constitution IV.1 puts TenantId first).
/// </remarks>
public class UOM : ITenantEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant; immutable after creation (Constitution Article II.4).</summary>
    public Guid TenantId { get; set; }

    /// <summary>Unique-within-tenant code, e.g. "EA", "KG" (max 20 chars).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Display name (max 50 chars).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Multiplier that converts one unit of this UOM into the item's Base UOM
    /// (decimal(18,6), must be strictly greater than zero).
    /// </summary>
    public decimal ToBaseFactor { get; set; }
}
