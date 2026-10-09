namespace Erp.Domain.Entities;

/// <summary>
/// Stable machine-readable failure codes for the Selling module (Task 5.1). They flow Domain
/// -&gt; Application (<c>Error.Code</c>) -&gt; Api, where the controllers map them to RFC 7807 status
/// codes (duplicates -&gt; 409, everything else -&gt; 400), mirroring
/// <see cref="PurchaseErrorCodes"/> and <see cref="AccountErrorCodes"/>.
/// </summary>
public static class SellingErrorCodes
{
    // Customer master (Task 5.1)
    public const string CompanyRequired = "company_required";
    public const string CustomerCodeRequired = "customer_code_required";
    public const string CustomerCodeTooLong = "customer_code_too_long";
    public const string CustomerNameRequired = "customer_name_required";
    public const string CustomerNameTooLong = "customer_name_too_long";
    public const string CustomerTaxIdTooLong = "customer_tax_id_too_long";
    public const string BillingCurrencyInvalid = "billing_currency_invalid";
    public const string InvalidCreditLimit = "invalid_credit_limit";
    public const string InvalidPaymentTermsDays = "invalid_payment_terms_days";
    public const string InvalidReceivableAccount = "invalid_receivable_account";
    public const string DuplicateCustomerCode = "duplicate_customer_code";
    public const string CustomerNotFound = "customer_not_found";

    // Customer ERPNext-parity fields (length guards; see CustomerValidator)
    public const string CustomerTypeTooLong = "customer_type_too_long";
    public const string CustomerGroupTooLong = "customer_group_too_long";
    public const string TerritoryTooLong = "territory_too_long";
    public const string BillingAddressTooLong = "billing_address_too_long";
    public const string PhoneTooLong = "phone_too_long";
    public const string EmailTooLong = "email_too_long";
    public const string ContactPersonTooLong = "contact_person_too_long";
    public const string WebsiteTooLong = "website_too_long";
    public const string PaymentTermsTooLong = "payment_terms_too_long";
    public const string CustomerDetailsTooLong = "customer_details_too_long";

    /// <summary>
    /// Credit-limit breach (spec SL-02 / plan.md §2): the command is rejected with
    /// <see cref="Exceptions.CreditLimitExceededException"/> and this is the code carried into the
    /// RFC 7807 <c>code</c> extension.
    /// </summary>
    public const string CreditLimitExceeded = "credit_limit_exceeded";

    // Sales order (Task 5.2)
    public const string SalesOrderNotFound = "sales_order_not_found";
    public const string InvalidStatusTransition = "invalid_status_transition";
    public const string SalesOrderNotDeliverable = "sales_order_not_deliverable";
    public const string CustomerRequired = "customer_required";
    public const string CustomerInactive = "customer_inactive";
    public const string TransactionDateRequired = "transaction_date_required";
    public const string DeliveryDateRequired = "delivery_date_required";
    public const string OrderNumberRequired = "order_number_required";
    public const string CompanyNotFound = "company_not_found";
    public const string NoLines = "no_lines";
    public const string InvalidQuantity = "invalid_quantity";
    public const string InvalidRate = "invalid_rate";
    public const string InvalidAmount = "invalid_amount";
    public const string InvalidTotals = "invalid_totals";
    public const string SalesOrderLineMismatch = "sales_order_line_mismatch";

    // Delivery note (Task 5.2b / Amendment A1)
    public const string DeliveryNoteNotFound = "delivery_note_not_found";
    public const string OverdeliveryNotAllowed = "overdelivery_not_allowed";

    // POS (Task 5.4)
    public const string POSProfileNotFound = "pos_profile_not_found";
    public const string ItemNotFound = "item_not_found";
    public const string ValidationFailed = "validation_failed";
}
