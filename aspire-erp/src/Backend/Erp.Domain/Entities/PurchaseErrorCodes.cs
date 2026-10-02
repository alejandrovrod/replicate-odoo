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
}
