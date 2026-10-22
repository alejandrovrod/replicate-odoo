namespace Erp.Application.Features.Tenants;

/// <summary>
/// Public onboarding read model (host-based tenant resolution): just enough for the SPA to
/// pin the tenant id and apply the tenant's default language before the authenticated shell
/// loads.
/// </summary>
public sealed record TenantLookupDto(
    Guid Id,
    string Name,
    string Subdomain,
    string DefaultLanguage);

/// <summary>Result of the live subdomain check used by the registration form.</summary>
public sealed record SubdomainAvailabilityDto(string Subdomain, bool Available);

/// <summary>
/// Successful self-service registration. Never carries the password: the administrator chose
/// it on the form and is signed in with it afterwards.
/// </summary>
public sealed record TenantRegistrationResult(
    Guid TenantId,
    Guid CompanyId,
    Guid AdminUserId,
    string TenantName,
    string Subdomain,
    string AdminEmail,
    string DefaultLanguage);
