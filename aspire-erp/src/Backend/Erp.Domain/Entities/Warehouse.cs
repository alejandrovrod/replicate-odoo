using Erp.Domain.Common;

namespace Erp.Domain.Entities;

/// <summary>
/// A physical or logical stock location, optionally grouped under a parent warehouse
/// (.specify/spec.md §4: "Warehouse (Hierarchical)"). Every warehouse owns exactly one
/// General Ledger account - the "linked stock account" that carries its inventory value.
/// </summary>
/// <remarks>
/// Tenant-scoped AND company-scoped: the tree rules mirror the Account tree (same company, no
/// cycles, only Group nodes may have children) and are enforced by <see cref="WarehouseValidator"/>.
/// </remarks>
public class Warehouse : ITenantEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant; immutable after creation (Constitution Article II.4).</summary>
    public Guid TenantId { get; set; }

    /// <summary>Company that owns this warehouse tree.</summary>
    public Guid CompanyId { get; set; }

    /// <summary>Unique-within-company code (max 50 chars), e.g. "SN".</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Display name (max 150 chars), e.g. "Stores - North".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Parent in the warehouse tree; null for a root-level warehouse.</summary>
    public Guid? ParentWarehouseId { get; set; }

    public Warehouse? Parent { get; set; }

    public ICollection<Warehouse> Children { get; set; } = new List<Warehouse>();

    /// <summary>
    /// GL account that holds the inventory value stored in this warehouse (required).
    /// Stock movements debit/credit this account (spec ST-01: 1310 - Stock In Hand).
    /// </summary>
    public Guid StockAccountId { get; set; }

    public Account? StockAccount { get; set; }

    /// <summary>Group = container node (no stock of its own by convention); leaf = real location.</summary>
    public bool IsGroup { get; set; }

    public bool IsActive { get; set; } = true;
}
