using Erp.Application.Common;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Tenants;

/// <summary>Public lookup used by the SPA boot to resolve a tenant from the current host.</summary>
public sealed record GetTenantBySubdomainQuery(string Subdomain)
    : IQuery<Result<TenantLookupDto>>;

public sealed class GetTenantBySubdomainQueryHandler
    : IQueryHandler<GetTenantBySubdomainQuery, Result<TenantLookupDto>>
{
    private readonly ITenantOnboardingRepository _tenants;

    public GetTenantBySubdomainQueryHandler(ITenantOnboardingRepository tenants)
    {
        _tenants = tenants;
    }

    public async Task<Result<TenantLookupDto>> HandleAsync(
        GetTenantBySubdomainQuery query,
        CancellationToken cancellationToken = default)
    {
        string subdomain;
        try
        {
            subdomain = TenantSubdomain.EnsureValid(query.Subdomain);
        }
        catch (AuthValidationException ex)
        {
            return Result<TenantLookupDto>.Failure(ex.Code, ex.Message);
        }

        var tenant = await _tenants.GetBySubdomainAsync(subdomain, cancellationToken);

        return tenant is null
            ? Result<TenantLookupDto>.Failure(
                TenantErrorCodes.TenantNotFound,
                $"No active tenant was found for subdomain '{subdomain}'.")
            : Result<TenantLookupDto>.Success(
                new TenantLookupDto(
                    tenant.Id,
                    tenant.Name,
                    tenant.Subdomain,
                    tenant.DefaultLanguage));
    }
}
