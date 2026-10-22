using Erp.Domain.Entities;
using Erp.Domain.Entities.Security;
using Erp.Domain.Exceptions;

namespace Erp.Application.Features.Tenants;

/// <summary>
/// Field-shape validation for <see cref="RegisterTenantCommand"/>. Subdomain-specific errors
/// are handled separately by the handler so they keep their precise wire codes
/// (<c>SUBDOMAIN_INVALID</c> / <c>SUBDOMAIN_RESERVED</c>); everything here maps to
/// <c>INVALID_TENANT_DATA</c> (400).
/// </summary>
public static class RegisterTenantCommandValidator
{
    public const int MaxTenantNameLength = 100;
    public const int MaxCompanyNameLength = 150;
    public const int MaxTaxIdLength = 50;

    public static IReadOnlyList<string> Validate(RegisterTenantCommand command)
    {
        var violations = new List<string>();

        if (string.IsNullOrWhiteSpace(command.TenantName))
        {
            violations.Add("Tenant name is required.");
        }
        else if (command.TenantName.Trim().Length > MaxTenantNameLength)
        {
            violations.Add($"Tenant name must not exceed {MaxTenantNameLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(command.CompanyName))
        {
            violations.Add("Company name is required.");
        }
        else if (command.CompanyName.Trim().Length > MaxCompanyNameLength)
        {
            violations.Add($"Company name must not exceed {MaxCompanyNameLength} characters.");
        }

        if (command.CompanyTaxId is { Length: > MaxTaxIdLength })
        {
            violations.Add($"Company tax id must not exceed {MaxTaxIdLength} characters.");
        }

        if (!TenantLanguage.IsSupported(command.DefaultLanguage))
        {
            violations.Add("Default language must be either 'en' or 'es'.");
        }

        try
        {
            UserValidator.EnsureValidFields(command.AdminFullName, command.AdminEmail);
        }
        catch (AuthValidationException ex)
        {
            violations.Add(ex.Message);
        }

        try
        {
            ProfileValidator.EnsureValidNewPassword(command.AdminPassword, command.AdminPassword);
        }
        catch (AuthValidationException ex)
        {
            violations.Add(ex.Message);
        }

        return violations;
    }
}
