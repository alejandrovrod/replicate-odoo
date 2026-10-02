namespace Erp.Domain.Exceptions;

/// <summary>
/// Raised when a purchase posting needs GL or company configuration that is missing or ambiguous
/// (Constitution III.3 sanity + decision D3 applied to the buying defaults): e.g.
/// <c>Company.AccountsPayableAccountCode</c> or <c>Company.InputTaxRecoverableAccountCode</c> not
/// resolving to exactly one active leaf account. This is a configuration fault, not a user input
/// fault - it must be fixed by seeding/configuration, so it deliberately does NOT map to a 4xx
/// domain failure code (mirrors <see cref="StockPostingConfigurationException"/>).
/// </summary>
public sealed class PurchasePostingConfigurationException : Exception
{
    public PurchasePostingConfigurationException(string message) : base(message)
    {
    }
}
