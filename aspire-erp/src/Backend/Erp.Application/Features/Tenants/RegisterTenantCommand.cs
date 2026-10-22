using Erp.Application.Common;
using Erp.Domain.Entities;

namespace Erp.Application.Features.Tenants;

/// <summary>
/// Self-service tenant onboarding: creates the tenant, its first company, the standard COA,
/// the starter master data, the RBAC matrix and the initial System Manager in one atomic
/// transaction.
/// </summary>
public sealed record RegisterTenantCommand(
    string TenantName,
    string CompanyName,
    string Subdomain,
    string AdminFullName,
    string AdminEmail,
    string AdminPassword,
    string? CompanyTaxId = null,
    string DefaultLanguage = TenantLanguage.Default) : ICommand<Result<TenantRegistrationResult>>;
