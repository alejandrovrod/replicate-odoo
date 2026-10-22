namespace Erp.Domain.Entities;

/// <summary>
/// Top-level isolation boundary representing an independent customer organization
/// (ubiquitous language: "Tenant" - see .specify/domain_business_rules_ddd.md §1).
/// </summary>
public class Tenant
{
    private string _defaultLanguage = TenantLanguage.Default;

    public Guid Id { get; set; }

    /// <summary>Human-readable organization name (max 100 chars).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Unique tenant code (max 50 chars).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Lowercase DNS label used for host-based tenant resolution
    /// (<c>{subdomain}.domain.com</c> or <c>{subdomain}.localhost</c>). Unique across tenants.
    /// Validation rules live in <see cref="TenantSubdomain"/>.
    /// </summary>
    public string Subdomain { get; set; } = string.Empty;

    /// <summary>
    /// Primary workspace language (<c>en</c> or <c>es</c>). Starter master data is seeded in this
    /// language and the SPA applies it on subdomain boot when the user has no manual preference.
    /// </summary>
    public string DefaultLanguage
    {
        get => _defaultLanguage;
        set => _defaultLanguage = TenantLanguage.EnsureValid(value);
    }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
}
