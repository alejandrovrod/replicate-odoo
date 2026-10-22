using Erp.Application.Common;
using Erp.Application.Services;
using Erp.Domain.Entities;
using Erp.Domain.Entities.Security;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Tenants;

/// <summary>
/// Executes <see cref="RegisterTenantCommand"/>: validates/normalizes the payload, hashes the
/// administrator password, and delegates the atomic write to
/// <see cref="ITenantOnboardingRepository"/>.
/// </summary>
public sealed class RegisterTenantCommandHandler
    : ICommandHandler<RegisterTenantCommand, Result<TenantRegistrationResult>>
{
    private readonly ITenantOnboardingRepository _tenants;
    private readonly IPasswordHasher _passwords;

    public RegisterTenantCommandHandler(
        ITenantOnboardingRepository tenants,
        IPasswordHasher passwords)
    {
        _tenants = tenants;
        _passwords = passwords;
    }

    public async Task<Result<TenantRegistrationResult>> HandleAsync(
        RegisterTenantCommand command,
        CancellationToken cancellationToken = default)
    {
        string subdomain;
        try
        {
            subdomain = TenantSubdomain.EnsureValid(command.Subdomain);
        }
        catch (AuthValidationException ex)
        {
            return Result<TenantRegistrationResult>.Failure(ex.Code, ex.Message);
        }

        var violations = RegisterTenantCommandValidator.Validate(command);
        if (violations.Count > 0)
        {
            return Result<TenantRegistrationResult>.Failure(
                TenantErrorCodes.InvalidTenantData,
                string.Join(" ", violations));
        }

        if (await _tenants.SubdomainExistsAsync(subdomain, cancellationToken))
        {
            return Result<TenantRegistrationResult>.Failure(
                TenantErrorCodes.SubdomainTaken,
                $"The subdomain '{subdomain}' is already in use.");
        }

        var adminEmail = UserValidator.NormalizeEmail(command.AdminEmail);
        var defaultLanguage = TenantLanguage.Resolve(command.DefaultLanguage);

        try
        {
            var ids = await _tenants.RegisterAsync(
                new TenantRegistrationData(
                    TenantName: command.TenantName.Trim(),
                    TenantCode: subdomain.ToUpperInvariant(),
                    Subdomain: subdomain,
                    CompanyName: command.CompanyName.Trim(),
                    CompanyTaxId: command.CompanyTaxId?.Trim() ?? string.Empty,
                    AdminFullName: command.AdminFullName.Trim(),
                    AdminEmail: adminEmail,
                    AdminPasswordHash: _passwords.Hash(command.AdminPassword),
                    DefaultLanguage: defaultLanguage),
                cancellationToken);

            return Result<TenantRegistrationResult>.Success(
                new TenantRegistrationResult(
                    ids.TenantId,
                    ids.CompanyId,
                    ids.AdminUserId,
                    command.TenantName.Trim(),
                    subdomain,
                    adminEmail,
                    defaultLanguage));
        }
        catch (AuthValidationException ex)
        {
            return Result<TenantRegistrationResult>.Failure(ex.Code, ex.Message);
        }
    }
}
