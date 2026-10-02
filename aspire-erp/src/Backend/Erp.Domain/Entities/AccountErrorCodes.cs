namespace Erp.Domain.Entities;

/// <summary>
/// Stable machine-readable failure codes for Account domain violations. They flow Domain ->
/// Application (<c>Error.Code</c>) -> Api, where the controller maps them to RFC 7807 status codes
/// (duplicate -> 409, everything else -> 400).
/// </summary>
public static class AccountErrorCodes
{
    public const string CompanyRequired = "company_required";
    public const string AccountCodeRequired = "account_code_required";
    public const string AccountCodeTooLong = "account_code_too_long";
    public const string AccountNameRequired = "account_name_required";
    public const string AccountNameTooLong = "account_name_too_long";
    public const string InvalidRootType = "invalid_root_type";
    public const string CurrencyInvalid = "currency_invalid";
    public const string ParentNotFound = "parent_not_found";
    public const string ParentIsSelf = "parent_is_self";
    public const string ParentNotInSameCompany = "parent_not_in_same_company";
    public const string ParentIsNotGroup = "parent_is_not_group";
    public const string RootTypeMismatch = "root_type_mismatch";
    public const string CycleDetected = "cycle_detected";
    public const string DuplicateAccountCode = "duplicate_account_code";
}
