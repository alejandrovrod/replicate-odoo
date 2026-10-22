using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.UnitTests.Fakes;

/// <summary>In-memory onboarding repository for handler/query tests.</summary>
public sealed class FakeTenantOnboardingRepository : ITenantOnboardingRepository
{
    public HashSet<string> ExistingSubdomains { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, Tenant> Tenants { get; } = new(StringComparer.Ordinal);

    public TenantRegistrationData? LastRegistration { get; private set; }

    public Guid NextTenantId { get; set; } = Guid.NewGuid();

    public Guid NextCompanyId { get; set; } = Guid.NewGuid();

    public Guid NextAdminUserId { get; set; } = Guid.NewGuid();

    public Exception? RegisterException { get; set; }

    public void SeedTenant(Tenant tenant)
    {
        Tenants[tenant.Subdomain] = tenant;
        ExistingSubdomains.Add(tenant.Subdomain);
    }

    public Task<bool> SubdomainExistsAsync(
        string subdomain,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ExistingSubdomains.Contains(subdomain));

    public Task<Tenant?> GetBySubdomainAsync(
        string subdomain,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Tenants.GetValueOrDefault(subdomain));

    public Task<TenantRegistrationIds> RegisterAsync(
        TenantRegistrationData data,
        CancellationToken cancellationToken = default)
    {
        LastRegistration = data;

        if (RegisterException is not null)
        {
            throw RegisterException;
        }

        ExistingSubdomains.Add(data.Subdomain);
        return Task.FromResult(
            new TenantRegistrationIds(NextTenantId, NextCompanyId, NextAdminUserId));
    }
}
