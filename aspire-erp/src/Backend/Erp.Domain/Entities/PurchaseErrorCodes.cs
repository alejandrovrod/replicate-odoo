namespace Erp.Domain.Entities;

/// <summary>
/// Stable machine-readable failure codes for the Buying module (Tasks 4.1-4.3). They flow Domain
/// -&gt; Application (<c>Error.Code</c>) -&gt; Api, where the controllers map them to RFC 7807 status
/// codes (duplicates and workflow conflicts -&gt; 409, everything else -&gt; 400), mirroring
/// <see cref="StockErrorCodes"/>. Values shared with the stock vocabulary keep the same wire
/// value so clients see one language for one concept.
/// </summary>
public static class PurchaseErrorCodes
{
    // Supplier (Task 4.1)
    public const string SupplierCodeRequired = "supplier_code_required";
    public const string SupplierCodeTooLong = "supplier_code_too_long";
    public const string SupplierNameRequired = "supplier_name_required";
    public const string SupplierNameTooLong = "supplier_name_too_long";
    public const string DuplicateSupplierCode = "duplicate_supplier_code";
    public const string SupplierNotFound = "supplier_not_found";
    public const string SupplierInactive = "supplier_inactive";
    public const string InvalidCurrency = "invalid_currency";
    public const string InvalidPaymentTerms = "invalid_payment_terms";

    // Supplier groups (module buying-suppliers, spec §1 gap D1)
    public const string SupplierGroupNotFound = "supplier_group_not_found";
    public const string DuplicateSupplierGroupCode = "duplicate_supplier_group_code";
    public const string SupplierGroupCodeRequired = "supplier_group_code_required";
    public const string SupplierGroupNameRequired = "supplier_group_name_required";

    // Supplier governance / hold (spec §1 gap D4, scenarios SUP-G2/SUP-G3).
    // On the wire these map to RFC 7807 422 Unprocessable Entity (the document is well-formed but
    // the supplier's state forbids it), unlike the field-level codes above (400).
    public const string SupplierOnHoldInvoices = "supplier_on_hold_invoices";
    public const string SupplierOnHoldPayments = "supplier_on_hold_payments";
    public const string SupplierOnHold = "supplier_on_hold";
    public const string SupplierFrozen = "supplier_frozen";

    // Purchase order (Task 4.1)
    public const string PurchaseOrderNotFound = "purchase_order_not_found";
    public const string InvalidStatusTransition = "invalid_status_transition";

    // Shared document field rules (same wire values as the stock vocabulary)
    public const string NoLines = "no_lines";
    public const string InvalidQuantity = "invalid_quantity";
    public const string InvalidRate = "invalid_rate";
    public const string CompanyNotFound = "company_not_found";
    public const string InvalidGlAccount = "invalid_gl_account";

    // Purchase receipt (Task 4.2)
    public const string PurchaseReceiptNotFound = "purchase_receipt_not_found";

    // Purchase invoice / three-way matching (Task 4.3)
    public const string InvoiceAlreadyExists = "invoice_already_exists";
    public const string ReceiptLineMismatch = "receipt_line_mismatch";
    public const string QuantityMismatch = "quantity_mismatch";
    public const string InvalidTaxAmount = "invalid_tax_amount";

    // Purchase returns / supplier debit notes (module 19, spec returns-and-credit-notes, T1).
    // Same wire values as the certified sales vocabulary (SellingErrorCodes) so one concept keeps
    // one language across both ledgers.
    public const string ReturnAgainstRequired = "return_against_required";
    public const string ReturnAgainstInvalid = "return_against_invalid";
    public const string ReturnAmountExceeded = "return_amount_exceeded";

    /// <summary>A return line requests a non-positive quantity (mirrors the sales rule).</summary>
    public const string ReturnLineInvalid = "return_line_invalid";

    /// <summary>
    /// Cancelling a supplier debit note is not supported yet (module 19, RET-G4 remains open): the
    /// certified cancellation path unwinds receipts and would corrupt the ledger for a return, so it
    /// is refused explicitly instead of being silently mishandled.
    /// </summary>
    public const string ReturnCancellationUnsupported = "return_cancellation_unsupported";

    // Three-way match overbilling rejection (Task 4.4 / spec BY-03 and BY-06): the invoice bills
    // more than the receipt line still has available. It is a bad REQUEST (400), not a conflict.
    public const string OverbillingNotAllowed = "overbilling_not_allowed";

    // Purchase invoice cancellation (Task 4.6 / spec BY-05)
    public const string InvoiceNotFound = "purchase_invoice_not_found";
    public const string InvoiceAlreadyCancelled = "invoice_already_cancelled";
    public const string InvoiceNotPosted = "invoice_not_posted";
}
