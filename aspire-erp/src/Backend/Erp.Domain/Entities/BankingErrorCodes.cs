namespace Erp.Domain.Entities;

/// <summary>
/// Stable machine-readable failure codes for the Banking &amp; Reconciliation module. They flow
/// Domain -&gt; Application (<c>Error.Code</c>) -&gt; Api, where the controller maps them to RFC 7807
/// status codes, mirroring StockErrorCodes.
/// </summary>
public static class BankingErrorCodes
{
    // Payment allocation (task 6.1 anti-overpayment invariant)
    public const string InvalidAllocationAmount = "invalid_allocation_amount";
    public const string OverAllocation = "over_allocation";

    // Statement import staging (task 6.2)
    public const string BankAccountNotFound = "bank_account_not_found";
    public const string InvalidStatementFormat = "invalid_statement_format";
    public const string EmptyStatement = "empty_statement";
    public const string MalformedCsvRow = "malformed_csv_row";
    public const string MalformedOfxBlock = "malformed_ofx_block";

    // Staging invariant BN-02 (deposit / withdrawal mutual exclusivity)
    public const string BothSidesPosted = "both_sides_posted";
    public const string NegativeTransactionAmount = "negative_transaction_amount";

    // Heuristic rules engine (task 6.3)
    public const string CompanyNotFound = "company_not_found";
    public const string BankTransactionRuleNotFound = "bank_transaction_rule_not_found";
    public const string InvalidRulePattern = "invalid_rule_pattern";

    // Reconciliation (task 6.4)
    public const string BankTransactionNotFound = "bank_transaction_not_found";
    public const string PaymentEntryNotFound = "payment_entry_not_found";
    public const string GlVoucherNotFound = "gl_voucher_not_found";
    public const string InvalidStatusTransition = "invalid_status_transition";
    public const string ReconciliationAmountMismatch = "reconciliation_amount_mismatch";
    public const string GlAmountMismatch = "gl_amount_mismatch";
    public const string InvalidReconciliationAmount = "invalid_reconciliation_amount";
}
