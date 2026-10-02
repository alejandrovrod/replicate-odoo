namespace Erp.Domain.Entities;

/// <summary>
/// Top-level isolation boundary representing an independent customer organization
/// (ubiquitous language: "Tenant" - see .specify/domain_business_rules_ddd.md §1).
/// </summary>
public class Tenant
{
    public Guid Id { get; set; }

    /// <summary>Human-readable organization name (max 100 chars).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Unique tenant code (max 50 chars).</summary>
    public string Code { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
}
