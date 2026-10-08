namespace Erp.Domain.Entities;

/// <summary>
/// Stable machine-readable failure codes for Currency domain violations. They flow Domain ->
/// Application (<c>Error.Code</c>) -> Api, where the controller maps them to RFC 7807 status codes
/// (duplicate/not-found -> 409/404, currency_not_found -> 404, everything else -> 400).
/// </summary>
public static class CurrencyErrorCodes
{
    public const string CurrencyNotFound = "currency_not_found";
    public const string CodeRequired = "currency_code_required";
    public const string CodeTooLong = "currency_code_too_long";
    public const string SymbolRequired = "currency_symbol_required";
    public const string SymbolTooLong = "currency_symbol_too_long";
    public const string FractionNameTooLong = "currency_fraction_name_too_long";
    public const string DuplicateCurrencyCode = "duplicate_currency_code";
}
