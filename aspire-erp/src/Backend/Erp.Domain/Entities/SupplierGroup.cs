using Erp.Domain.Common;

namespace Erp.Domain.Entities;

/// <summary>
/// Supplier classification catalog (spec §1 gap D1, ERPNext parity <c>supplier_group</c>):
/// Local, Exterior, Servicios, Materias Primas, etc. Tenants use it to parameterise buying and,
/// later, accounting defaults.
/// </summary>
/// <remarks>
/// Tenant-scoped MASTER entity (like <see cref="Supplier"/>): codes are unique per TENANT and the
/// global query filter scopes reads automatically (Constitution II.3). NOT temporal and NOT
/// system-versioned, following the Supplier/Item master precedent.
/// </remarks>
public class SupplierGroup : ITenantEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant; immutable after creation (Constitution Article II.4).</summary>
    public Guid TenantId { get; set; }

    /// <summary>Group code, unique per tenant (max 50 chars).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Display name (max 150 chars).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Inactive groups stay for historical rows but are not offered for new suppliers.</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
}
