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

    // On-the-fly voucher dialog (task 6.5)
    public const string ExpenseAccountNotFound = "expense_account_not_found";
    public const string BankGlAccountNotFound = "bank_gl_account_not_found";

    // Payment entry & settlement (spec R-12)
    public const string PaymentNotFound = "payment_not_found";
    public const string InvalidPaidAmount = "invalid_paid_amount";
    public const string PaymentConservationViolated = "payment_conservation_violated";
    public const string PaymentPartyMismatch = "payment_party_mismatch";
    public const string PaymentInvalidTransition = "payment_invalid_transition";
    public const string PaymentAlreadyCancelled = "payment_already_cancelled";
    public const string PaymentReconciledCannotCancel = "payment_reconciled_cannot_cancel";
    public const string InvalidCounterpartyAccount = "invalid_counterparty_account";

    // Period closing voucher (spec R-13)
    public const string PeriodClosingNotFound = "period_closing_not_found";
    public const string RetainedEarningsAccountNotFound = "retained_earnings_account_not_found";
    public const string InvalidPeriodClosingAmount = "invalid_period_closing_amount";
    public const string PeriodClosingConservationViolated = "period_closing_conservation_violated";
    public const string PeriodClosingInvalidTransition = "period_closing_invalid_transition";
    public const string PeriodClosingAlreadyCancelled = "period_closing_already_cancelled";
    public const string PeriodClosingReconciledCannotCancel = "period_closing_reconciled_cannot_cancel";

    // Bank account master (CRUD)
    public const string BankAccountNameRequired = "bank_account_name_required";
    public const string BankNameRequired = "bank_name_required";
    public const string BankAccountNumberRequired = "bank_account_number_required";
    public const string InvalidGlAccount = "invalid_gl_account";
}
