namespace Erp.Domain.Entities;

/// <summary>
/// Stable machine-readable failure codes owned by the Accounting module (01-accounting). They
/// flow Domain -&gt; Application (<c>Error.Code</c>) -&gt; Api, where the controllers map them to
/// RFC 7807 status codes, mirroring <see cref="StockErrorCodes"/>/<see cref="PurchaseErrorCodes"/>.
/// </summary>
/// <remarks>
/// <c>FiscalPeriodLocked</c> lives HERE (and not in <see cref="StockErrorCodes"/>) because the
/// hard period lock (spec AC-04) is an accounting-domain rule shared by every posting pipeline -
/// stock, buying and the upcoming JournalEntry pipeline (tasks.md 2.3). Referencing the stock
/// vocabulary from a journal posting would be a layering inversion.
/// </remarks>
public static class AccountingErrorCodes
{
    /// <summary>
    /// PostingDate &lt;= Company.FrozenAccountsDate: the fiscal period is closed
    /// (spec AC-04 / plan.md §3). Mapped to HTTP 409 by the posting controllers - the request
    /// conflicts with the state of the fiscal calendar, the same class of conflict as
    /// <c>invalid_status_transition</c> and <c>concurrency_conflict</c>.
    /// </summary>
    public const string FiscalPeriodLocked = "fiscal_period_locked";

    /// <summary>
    /// A voucher line targets an <c>Account.IsGroup == true</c> node, which is a folder of the COA
    /// and never a posting account (spec AC-03 / spec.md §1 "Group Account"). Mapped to HTTP 400
    /// by the posting controllers: the request itself names an account that may not receive
    /// ledger rows, i.e. a bad request rather than a state conflict.
    /// </summary>
    /// <remarks>
    /// Wire value is the snake_case spelling of spec AC-03's literal error token
    /// <c>PostingToGroupAccountProhibited</c>. The spec writes the token in PascalCase because it
    /// names the CONDITION; every machine code this codebase emits is snake_case
    /// (<c>fiscal_period_locked</c>, <c>invalid_status_transition</c>, <c>double_entry_imbalance</c>),
    /// so AC-03's token is surfaced as <c>posting_to_group_account_prohibited</c> - same mapping
    /// rule the rest of the error vocabulary already follows.
    /// </remarks>
    public const string PostingToGroupAccountProhibited = "posting_to_group_account_prohibited";
}
