using System.Text.RegularExpressions;
using Erp.Domain.Exceptions;

namespace Erp.Domain.Entities;

/// <summary>
/// Domain rules for the Lead entity.
/// </summary>
public static class LeadValidator
{
    public const int MaxCodeLength = 50;
    public const int MaxNameLength = 150;
    public const int MaxOrganizationLength = 150;
    public const int MaxEmailLength = 150;
    public const int MaxPhoneLength = 50;
    public const int MaxSourceLength = 50;

    // Simple robust regex for standard email validation
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$", 
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Standard phone validation (allows +, spaces, dashes, parentheses and digits)
    private static readonly Regex PhoneRegex = new(
        @"^[+]?[(]?[0-9]{1,4}[)]?[-\s\./0-9]*$", 
        RegexOptions.Compiled);

    /// <summary>
    /// Ensures all basic Lead field rules are met.
    /// </summary>
    public static void EnsureValidLeadFields(
        Guid companyId,
        string? leadCode,
        string? leadName,
        string? email,
        string? phone,
        string? source)
    {
        if (companyId == Guid.Empty)
        {
            throw new CRMValidationException(
                CRMErrorCodes.CompanyRequired,
                "A lead must belong to a company (CompanyId is required).");
        }

        if (string.IsNullOrWhiteSpace(leadCode))
        {
            throw new CRMValidationException(
                CRMErrorCodes.LeadCodeRequired,
                "Lead Code is required.");
        }

        if (leadCode.Length > MaxCodeLength)
        {
            throw new CRMValidationException(
                CRMErrorCodes.LeadCodeTooLong,
                $"Lead Code must not exceed {MaxCodeLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(leadName))
        {
            throw new CRMValidationException(
                CRMErrorCodes.LeadNameRequired,
                "Lead Name is required.");
        }

        if (leadName.Length > MaxNameLength)
        {
            throw new CRMValidationException(
                CRMErrorCodes.LeadNameTooLong,
                $"Lead Name must not exceed {MaxNameLength} characters.");
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            if (email.Length > MaxEmailLength)
            {
                throw new CRMValidationException(
                    CRMErrorCodes.InvalidEmail,
                    $"Email must not exceed {MaxEmailLength} characters.");
            }

            if (!EmailRegex.IsMatch(email))
            {
                throw new CRMValidationException(
                    CRMErrorCodes.InvalidEmail,
                    $"Email format is invalid: {email}");
            }
        }

        if (!string.IsNullOrWhiteSpace(phone))
        {
            if (phone.Length > MaxPhoneLength)
            {
                throw new CRMValidationException(
                    CRMErrorCodes.InvalidPhone,
                    $"Phone must not exceed {MaxPhoneLength} characters.");
            }

            if (!PhoneRegex.IsMatch(phone))
            {
                throw new CRMValidationException(
                    CRMErrorCodes.InvalidPhone,
                    $"Phone format is invalid: {phone}");
            }
        }

        if (string.IsNullOrWhiteSpace(source) || source.Length > MaxSourceLength)
        {
            throw new CRMValidationException(
                CRMErrorCodes.InvalidSource,
                $"Source must not be empty and must not exceed {MaxSourceLength} characters.");
        }
    }
}
