using Erp.Application.Common;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Tenants;

/// <summary>
/// Live availability probe for the registration form. Invalid and reserved names are simply
/// unavailable; the registration command returns the precise error code when it is submitted.
/// </summary>
public sealed record CheckSubdomainAvailabilityQuery(string Subdomain) : IQuery<bool>;

public sealed class CheckSubdomainAvailabilityQueryHandler
    : IQueryHandler<CheckSubdomainAvailabilityQuery, bool>
{
    private readonly ITenantOnboardingRepository _tenants;

    public CheckSubdomainAvailabilityQueryHandler(ITenantOnboardingRepository tenants)
    {
        _tenants = tenants;
    }

    public async Task<bool> HandleAsync(
        CheckSubdomainAvailabilityQuery query,
        CancellationToken cancellationToken = default)
    {
        var subdomain = TenantSubdomain.Normalize(query.Subdomain);

        if (!TenantSubdomain.IsValid(subdomain))
        {
            return false;
        }

        return !await _tenants.SubdomainExistsAsync(subdomain, cancellationToken);
    }
}
