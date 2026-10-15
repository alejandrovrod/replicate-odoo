using Erp.Domain.Exceptions;

namespace Erp.Domain.Entities;

/// <summary>
/// Pure C# field and workflow rules for the Buying module (Tasks 4.1-4.3). No EF Core, no NuGet
/// packages - Constitution Article I.2 keeps Erp.Domain dependency-free, so every rule here is
/// unit-tested without a database (mirrors <see cref="StockEntryValidator"/>).
/// </summary>
public static class PurchaseValidator
{
    public const int MaxCodeLength = 50;
    public const int MaxNameLength = 150;

    /// <summary>Supplier field rules: required code (50) / name (150).</summary>
    /// <remarks>
    /// Currency travels as <c>CurrencyId</c> (RM-09 FK to the global catalog, validated for
    /// existence by the command handler) - the ISO-format check was removed with the string column.
    /// </remarks>
    /// <exception cref="PurchaseValidationException">An invariant was violated.</exception>
    public static void EnsureValidSupplierFields(string? code, string? name, int paymentTerms)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new PurchaseValidationException(
                PurchaseErrorCodes.SupplierCodeRequired,
                "Supplier Code is required.");
        }

        if (code.Length > MaxCodeLength)
        {
            throw new PurchaseValidationException(
                PurchaseErrorCodes.SupplierCodeTooLong,
                $"Supplier Code must not exceed {MaxCodeLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new PurchaseValidationException(
                PurchaseErrorCodes.SupplierNameRequired,
                "Supplier Name is required.");
        }

        if (name.Length > MaxNameLength)
        {
            throw new PurchaseValidationException(
                PurchaseErrorCodes.SupplierNameTooLong,
                $"Supplier Name must not exceed {MaxNameLength} characters.");
        }

        if (paymentTerms < 0)
        {
            throw new PurchaseValidationException(
                PurchaseErrorCodes.InvalidPaymentTerms,
                "Payment Terms must not be negative.");
        }
    }

    /// <summary>A purchase document (order or receipt) must carry at least one line.</summary>
    /// <exception cref="PurchaseValidationException">The line list is empty.</exception>
    public static void EnsureHasLines(IReadOnlyCollection<object>? lines)
    {
        if (lines is null || lines.Count == 0)
        {
            throw new PurchaseValidationException(
                PurchaseErrorCodes.NoLines,
                "A purchase document must contain at least one line.");
        }
    }

    /// <summary>
    /// Order/receipt line rules: quantity strictly positive (decimal(18,4)) and a unit rate &gt; 0
    /// (decimal(18,6)). Orders fix the committed price; receipts value the incoming stock (ST-01).
    /// </summary>
    /// <exception cref="PurchaseValidationException">An invariant was violated.</exception>
    public static void EnsureValidLine(decimal qty, decimal rate)
    {
        if (qty <= 0)
        {
            throw new PurchaseValidationException(
                PurchaseErrorCodes.InvalidQuantity,
                $"Line quantity must be greater than zero (received {qty:0.####}).");
        }

        if (rate <= 0)
        {
            throw new PurchaseValidationException(
                PurchaseErrorCodes.InvalidRate,
                $"Line rate must be greater than zero (received {rate:0.######}).");
        }
    }

    /// <summary>
    /// Invoice line rules: quantity strictly positive (it must equal the receipt line's quantity
    /// - checked by the posting engine against the loaded receipt), rate &gt;= 0 (a vendor may bill
    /// a line at zero; the receipt value is what clears the interim liability).
    /// </summary>
    /// <exception cref="PurchaseValidationException">An invariant was violated.</exception>
    public static void EnsureValidInvoiceLine(decimal qty, decimal rate)
    {
        if (qty <= 0)
        {
            throw new PurchaseValidationException(
                PurchaseErrorCodes.InvalidQuantity,
                $"Line quantity must be greater than zero (received {qty:0.####}).");
        }

        if (rate < 0)
        {
            throw new PurchaseValidationException(
                PurchaseErrorCodes.InvalidRate,
                $"Line rate must not be negative (received {rate:0.######}).");
        }
    }

    /// <summary>Invoice tax rule: the vendor-stated Input Tax Recoverable total cannot be negative.</summary>
    /// <exception cref="PurchaseValidationException">The amount is negative.</exception>
    public static void EnsureValidTaxAmount(decimal taxAmount)
    {
        if (taxAmount < 0)
        {
            throw new PurchaseValidationException(
                PurchaseErrorCodes.InvalidTaxAmount,
                $"TaxAmount must not be negative (received {taxAmount:0.####}).");
        }
    }

    /// <summary>
    /// Return (supplier debit note) line rule (module 19, T2): the REQUESTED quantity is strictly
    /// positive - the posting engine stores the negative counterpart and enforces the per-line
    /// ceiling against the original bill.
    /// </summary>
    /// <exception cref="PurchaseValidationException">The requested quantity is not strictly positive.</exception>
    public static void EnsureValidReturnLine(decimal qty)
    {
        if (qty <= 0)
        {
            throw new PurchaseValidationException(
                PurchaseErrorCodes.ReturnLineInvalid,
                $"Return line quantity must be greater than zero (received {qty:0.####}).");
        }
    }

    /// <summary>
    /// Return provenance rule (module 19, T2): the target bill must exist, must itself be a normal
    /// (non-return) bill and must not be cancelled. Pure rule (no EF). Run BEFORE the line loop so a
    /// return-of-a-return fails with its own code instead of a downstream ceiling comparison.
    /// </summary>
    /// <exception cref="PurchaseValidationException">
    /// <see cref="PurchaseErrorCodes.ReturnAgainstRequired"/> when <paramref name="original"/> is null,
    /// <see cref="PurchaseErrorCodes.ReturnAgainstInvalid"/> for a return-of-a-return or a cancelled bill.
    /// </exception>
    public static void EnsureReturnTarget(PurchaseInvoice? original)
    {
        if (original is null)
        {
            throw new PurchaseValidationException(
                PurchaseErrorCodes.ReturnAgainstRequired,
                "A purchase return must reference an existing, posted bill.");
        }

        if (original.IsReturn)
        {
            throw new PurchaseValidationException(
                PurchaseErrorCodes.ReturnAgainstInvalid,
                $"Bill '{original.VoucherNo}' is itself a return; returns cannot be chained.");
        }

        if (original.Status == PurchaseInvoiceStatus.Cancelled)
        {
            throw new PurchaseValidationException(
                PurchaseErrorCodes.ReturnAgainstInvalid,
                $"Bill '{original.VoucherNo}' is cancelled and cannot be returned against.");
        }
    }

    /// <summary>
    /// Return cumulative ceiling rule (module 19, T2 / scenario RET-G3): the new return's gross,
    /// added to every debit note already booked against the same bill, cannot exceed the bill's
    /// gross. Pure rule (no EF): the caller loads the already-returned amount.
    /// </summary>
    /// <exception cref="PurchaseValidationException"><see cref="PurchaseErrorCodes.ReturnAmountExceeded"/>.</exception>
    public static void EnsureReturnAmountWithinCeiling(
        PurchaseInvoice original,
        decimal alreadyReturnedAmount,
        decimal returnAmount)
    {
        if (returnAmount <= 0)
        {
            throw new PurchaseValidationException(
                PurchaseErrorCodes.ReturnAmountExceeded,
                $"Return amount must be greater than zero (received {returnAmount:0.####}).");
        }

        var ceiling = Math.Abs(original.GrandTotal);
        if (Round4(alreadyReturnedAmount + returnAmount) > ceiling)
        {
            throw new PurchaseValidationException(
                PurchaseErrorCodes.ReturnAmountExceeded,
                $"Return of {returnAmount:0.####} exceeds what bill '{original.VoucherNo}' still has "
                + $"available: already returned {alreadyReturnedAmount:0.####} of {ceiling:0.####}.");
        }
    }

    private static decimal Round4(decimal value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);

    /// <summary>Workflow rule (Task 4.1): only a Draft order can be updated.</summary>
    /// <exception cref="PurchaseValidationException">The order is not in Draft.</exception>
    public static void EnsureDraft(PurchaseOrderStatus status)
    {
        if (status != PurchaseOrderStatus.Draft)
        {
            throw new PurchaseValidationException(
                PurchaseErrorCodes.InvalidStatusTransition,
                $"Only a Draft purchase order can be modified; the order is '{status}'.");
        }
    }

    /// <summary>Workflow rule (Task 4.1): only a Draft order can be submitted to Submitted.</summary>
    /// <exception cref="PurchaseValidationException">The order is not in Draft.</exception>
    public static void EnsureSubmittable(PurchaseOrderStatus status)
    {
        if (status != PurchaseOrderStatus.Draft)
        {
            throw new PurchaseValidationException(
                PurchaseErrorCodes.InvalidStatusTransition,
                $"Only a Draft purchase order can be submitted; the order is '{status}'.");
        }
    }

    /// <summary>
    /// Workflow rule (Task 4.1): a receipt may reference an order that is Submitted or already
    /// PartiallyReceived (multiple receipts per order).
    /// </summary>
    /// <exception cref="PurchaseValidationException">The order does not accept receipts.</exception>
    public static void EnsureReceiptAllowed(PurchaseOrderStatus status)
    {
        if (status is not (PurchaseOrderStatus.Submitted or PurchaseOrderStatus.PartiallyReceived))
        {
            throw new PurchaseValidationException(
                PurchaseErrorCodes.InvalidStatusTransition,
                $"A purchase receipt requires a Submitted or PartiallyReceived order; the order is '{status}'.");
        }
    }
}
