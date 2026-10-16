namespace Erp.Domain.Entities;

/// <summary>
/// Stable machine-readable failure codes for landed cost vouchers (Fase 4). They flow Domain
/// -&gt; Application (<c>Error.Code</c>) -&gt; Api, where the controller maps them to RFC 7807
/// ProblemDetails - mirroring <see cref="StockErrorCodes"/>.
/// </summary>
public static class LandedCostErrorCodes
{
    /// <summary>A referenced purchase receipt is not Submitted (LCV-G4).</summary>
    public const string PurchaseReceiptNotSubmitted = "purchase_receipt_not_submitted";

    /// <summary>No charge, or no charge above zero.</summary>
    public const string ChargesRequired = "charges_required";

    /// <summary>A charge row without a GL account.</summary>
    public const string ExpenseAccountRequired = "expense_account_required";

    public const string LandedCostVoucherNotFound = "landed_cost_voucher_not_found";

    /// <summary>Submit/cancel from the wrong lifecycle state.</summary>
    public const string InvalidStatusTransition = "invalid_status_transition";

    /// <summary>Unknown apportionment strategy or unspreadable weights.</summary>
    public const string InvalidDistributeBy = "invalid_distribute_by";

    /// <summary>No open stock layers to attach the revaluation to (everything sold).</summary>
    public const string NoStockToRevalue = "no_stock_to_revalue";
}
