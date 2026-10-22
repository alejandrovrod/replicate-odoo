using Erp.Domain.Entities;

namespace Erp.Domain.Repositories;

/// <summary>
/// Data-access contract for self-service tenant onboarding. The call only needs to exist
/// outside a tenant context (the public registration endpoint), so the implementation is
/// responsible for pinning the tenant provider once the new tenant has an id.
/// </summary>
public interface ITenantOnboardingRepository
{
    /// <summary>
    /// Any tenant - active or not - already owns this subdomain. Inactive tenants still count
    /// because the unique index applies to every row.
    /// </summary>
    Task<bool> SubdomainExistsAsync(
        string subdomain,
        CancellationToken cancellationToken = default);

    /// <summary>The ACTIVE tenant resolved by subdomain, or null.</summary>
    Task<Tenant?> GetBySubdomainAsync(
        string subdomain,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically creates the tenant, its first company, the standard Chart of Accounts, the
    /// starter UOM/warehouse/asset-category catalogs, the base RBAC matrix and the initial System
    /// Manager. The tenant's default language drives every localized name. The transaction
    /// commits only when every step succeeds; any failure rolls the whole onboarding back.
    /// </summary>
    Task<TenantRegistrationIds> RegisterAsync(
        TenantRegistrationData data,
        CancellationToken cancellationToken = default);
}

/// <summary>Validated onboarding payload handed to persistence (password already hashed).</summary>
public sealed record TenantRegistrationData(
    string TenantName,
    string TenantCode,
    string Subdomain,
    string CompanyName,
    string CompanyTaxId,
    string AdminFullName,
    string AdminEmail,
    string AdminPasswordHash,
    string DefaultLanguage = TenantLanguage.Default);

/// <summary>Identifiers produced by a successful atomic onboarding.</summary>
public sealed record TenantRegistrationIds(
    Guid TenantId,
    Guid CompanyId,
    Guid AdminUserId);
