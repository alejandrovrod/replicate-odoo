using Erp.Domain.Common;

namespace Erp.Domain.Entities;

/// <summary>
/// A legal tax entity operating under a Tenant (ubiquitous language: "Company" - see
/// .specify/domain_business_rules_ddd.md §1). Holds its own Chart of Accounts, currency and tax ID.
/// </summary>
/// <remarks>
/// Tenant-scoped: implements <c>ITenantEntity</c> (Constitution Article II.1). The value is
/// assigned automatically on insert and can never be altered afterwards (Constitution Article II.4).
/// </remarks>
public class Company : ITenantEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant; immutable after creation (Constitution Article II.4).</summary>
    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string DefaultCurrency { get; set; } = "USD";

    public string TaxId { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public Tenant? Tenant { get; set; }
}
