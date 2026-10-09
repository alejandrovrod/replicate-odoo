using Erp.Domain.Exceptions;

namespace Erp.Domain.Entities;

/// <summary>
/// Pure C# field rules for the Customer master (Task 5.1, plan.md §1 DDL). No EF Core, no NuGet
/// packages - Constitution Article I.2 keeps Erp.Domain dependency-free, so every rule here is
/// unit-tested without a database (mirrors <see cref="AccountValidator"/> and
/// <see cref="PurchaseValidator"/>).
/// </summary>
/// <remarks>
/// The lengths and ranges below are exactly the ones plan.md §1 enforces at the physical level
/// (NVARCHAR(50)/NVARCHAR(150) columns and <c>CK_Customer_CreditLimit</c>): catching them here
/// turns a SQL error into an RFC 7807 400 with a stable snake_case code.
/// </remarks>
public static class CustomerValidator
{
    public const int MaxCodeLength = 50;
    public const int MaxNameLength = 150;
    public const int MaxTaxIdLength = 50;
    public const int MaxCustomerTypeLength = 50;
    public const int MaxCustomerGroupLength = 100;
    public const int MaxTerritoryLength = 100;
    public const int MaxBillingAddressLength = 500;
    public const int MaxPhoneLength = 50;
    public const int MaxEmailLength = 150;
    public const int MaxContactPersonLength = 150;
    public const int MaxWebsiteLength = 200;
    public const int MaxPaymentTermsLength = 100;
    public const int MaxCustomerDetailsLength = 1000;

    /// <summary>
    /// Customer field rules: owning company (plan.md §1 CompanyId NOT NULL), required code (50) /
    /// name (150), TaxId at most 50 chars, a non-negative CreditLimit (CK_Customer_CreditLimit)
    /// and non-negative payment terms. Currency travels as <c>CurrencyId</c> (RM-09 FK, validated
    /// for existence by the command handler).
    /// </summary>
    /// <exception cref="CustomerValidationException">An invariant was violated.</exception>
    public static void EnsureValidCustomerFields(
        Guid companyId,
        string? customerCode,
        string? customerName,
        string? taxId,
        decimal creditLimit,
        int paymentTermsDays,
        string? customerType = null,
        string? customerGroup = null,
        string? territory = null,
        string? billingAddress = null,
        string? phone = null,
        string? email = null,
        string? contactPerson = null,
        string? website = null,
        string? paymentTerms = null,
        string? customerDetails = null)
    {
        if (companyId == Guid.Empty)
        {
            throw new CustomerValidationException(
                SellingErrorCodes.CompanyRequired,
                "A customer must belong to a company (CompanyId is required).");
        }

        if (string.IsNullOrWhiteSpace(customerCode))
        {
            throw new CustomerValidationException(
                SellingErrorCodes.CustomerCodeRequired,
                "Customer Code is required.");
        }

        if (customerCode.Length > MaxCodeLength)
        {
            throw new CustomerValidationException(
                SellingErrorCodes.CustomerCodeTooLong,
                $"Customer Code must not exceed {MaxCodeLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(customerName))
        {
            throw new CustomerValidationException(
                SellingErrorCodes.CustomerNameRequired,
                "Customer Name is required.");
        }

        if (customerName.Length > MaxNameLength)
        {
            throw new CustomerValidationException(
                SellingErrorCodes.CustomerNameTooLong,
                $"Customer Name must not exceed {MaxNameLength} characters.");
        }

        if (taxId is { Length: > MaxTaxIdLength })
        {
            throw new CustomerValidationException(
                SellingErrorCodes.CustomerTaxIdTooLong,
                $"TaxId must not exceed {MaxTaxIdLength} characters.");
        }

        if (creditLimit < 0)
        {
            throw new CustomerValidationException(
                SellingErrorCodes.InvalidCreditLimit,
                $"CreditLimit must not be negative (received {creditLimit:0.####}).");
        }

        if (paymentTermsDays < 0)
        {
            throw new CustomerValidationException(
                SellingErrorCodes.InvalidPaymentTermsDays,
                $"PaymentTermsDays must not be negative (received {paymentTermsDays}).");
        }

        EnsureMaxLength(customerType, MaxCustomerTypeLength, SellingErrorCodes.CustomerTypeTooLong, nameof(customerType));
        EnsureMaxLength(customerGroup, MaxCustomerGroupLength, SellingErrorCodes.CustomerGroupTooLong, nameof(customerGroup));
        EnsureMaxLength(territory, MaxTerritoryLength, SellingErrorCodes.TerritoryTooLong, nameof(territory));
        EnsureMaxLength(billingAddress, MaxBillingAddressLength, SellingErrorCodes.BillingAddressTooLong, nameof(billingAddress));
        EnsureMaxLength(phone, MaxPhoneLength, SellingErrorCodes.PhoneTooLong, nameof(phone));
        EnsureMaxLength(email, MaxEmailLength, SellingErrorCodes.EmailTooLong, nameof(email));
        EnsureMaxLength(contactPerson, MaxContactPersonLength, SellingErrorCodes.ContactPersonTooLong, nameof(contactPerson));
        EnsureMaxLength(website, MaxWebsiteLength, SellingErrorCodes.WebsiteTooLong, nameof(website));
        EnsureMaxLength(paymentTerms, MaxPaymentTermsLength, SellingErrorCodes.PaymentTermsTooLong, nameof(paymentTerms));
        EnsureMaxLength(customerDetails, MaxCustomerDetailsLength, SellingErrorCodes.CustomerDetailsTooLong, nameof(customerDetails));
    }

    private static void EnsureMaxLength(string? value, int maxLength, string code, string fieldName)
    {
        if (value is { Length: var length } && length > maxLength)
        {
            throw new CustomerValidationException(
                code,
                $"{fieldName} must not exceed {maxLength} characters.");
        }
    }
}
