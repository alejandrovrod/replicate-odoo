using System;
using AssetHub.Domain.Tenancy;

namespace AssetHub.Application.Interfaces;

public interface ITenantResolver
{
    Tenant? GetCurrentTenant();
    Guid? GetCurrentTenantId();
    Guid? GetCurrentUserId();
}
