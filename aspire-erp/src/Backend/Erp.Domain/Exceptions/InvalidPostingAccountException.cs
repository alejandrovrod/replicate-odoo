namespace Erp.Domain.Exceptions;

/// <summary>
/// Raised when a voucher line targets a GROUP account - a non-posting folder of the Chart of
/// Accounts (spec AC-03 / spec.md §1 "Group Account": "Direct postings are strictly forbidden").
/// Thrown by the Journal Entry posting pipeline BEFORE a single GLEntry row is built, so the
/// rejected submission writes zero ledger rows (AC-02's "zero records" guarantee applies to every
/// rejection, not only to imbalance).
/// </summary>
/// <remarks>
/// plan.md §1 lists this exception in <c>Erp.Domain/Exceptions</c> and plan.md §3 shows its exact
/// shape: <c>new InvalidPostingAccountException(account.AccountCode, account.AccountName)</c>.
/// Carries both strings so the RFC 7807 <c>detail</c> can name the offending account without the
/// client having to resolve it again.
/// </remarks>
public sealed class InvalidPostingAccountException : Exception
{
    /// <summary>
    /// Stable failure code carried into the RFC 7807 <c>code</c> extension: the snake_case
    /// spelling of spec AC-03's literal token <c>PostingToGroupAccountProhibited</c> (see
    /// <see cref="Entities.AccountingErrorCodes.PostingToGroupAccountProhibited"/> for the
    /// rationale of the case mapping).
    /// </summary>
    public string Code { get; } = Entities.AccountingErrorCodes.PostingToGroupAccountProhibited;

    /// <summary>Code of the group account that rejected the posting.</summary>
    public string AccountCode { get; }

    /// <summary>Name of the group account that rejected the posting.</summary>
    public string AccountName { get; }

    public InvalidPostingAccountException(string accountCode, string accountName)
        : base(
            $"Posting to group account prohibited: account '{accountCode}' - {accountName} is a "
            + "group (non-posting) account. Post to a leaf account instead "
            + "(spec AC-03 / plan.md §3).")
    {
        AccountCode = accountCode;
        AccountName = accountName;
    }
}
