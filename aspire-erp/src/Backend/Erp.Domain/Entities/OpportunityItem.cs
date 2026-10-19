using Erp.Domain.Common;

namespace Erp.Domain.Entities;

/// <summary>
/// One priced line of a commercial deal (CRM/ERPNext parity): opportunities carry their
/// own product/service breakdown instead of a single flat amount. The header
/// <see cref="Opportunity.OpportunityAmount"/> is recomputed from these lines by
/// <see cref="Opportunity.RecalculateTotals"/> whenever lines exist.
/// </summary>
public sealed class OpportunityItem : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public Guid OpportunityId { get; set; }
    public Opportunity? Opportunity { get; set; }

    public Guid ItemId { get; set; }
    public Item? Item { get; set; }

    /// <summary>Denormalized item name at pricing time (catalog renames never rewrite history).</summary>
    public string ItemName { get; set; } = string.Empty;

    public decimal Quantity { get; set; }
    public decimal Rate { get; set; }

    /// <summary>Line discount, 0-100 (ERPNext per-row discount parity).</summary>
    public decimal DiscountPercentage { get; set; }

    /// <summary>Net line value: quantity × rate × (1 - discount/100).</summary>
    public decimal Amount => Quantity * Rate * (1m - DiscountPercentage / 100m);
}
