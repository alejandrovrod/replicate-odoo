using Erp.Domain.Exceptions;

namespace Erp.Domain.Entities;

/// <summary>
/// Pure C# field and workflow rules for the Selling documents (Tasks 5.2/5.2b). No EF Core, no
/// NuGet packages - Constitution Article I.2 keeps Erp.Domain dependency-free, so every rule here
/// is unit-tested without a database (mirrors <see cref="PurchaseValidator"/> and
/// <see cref="CustomerValidator"/>).
/// </summary>
/// <remarks>
/// The ranges below are exactly the ones plan.md §1/§1.6 enforce at the physical level
/// (NVARCHAR(50) numbers, <c>CK_SalesOrder_Totals</c>, <c>CK_SalesOrderItem_Quantity/Rate/Amount</c>
/// and <c>CK_DeliveryNoteLine_Qty</c>): catching them here turns a SQL error into an RFC 7807
/// response with a stable snake_case code. Note the selling lines allow a ZERO rate (a free item)
/// where buying demands <c>Rate &gt; 0</c> - plan.md §1 says <c>Rate &gt;= 0.0000</c>.
/// </remarks>
public static class SalesValidator
{
    public const int MaxOrderNumberLength = 50;
    public const int MaxVoucherNoLength = 50;

    /// <summary>
    /// Sales order request rules: a customer (plan.md §1 FK_SalesOrder_Customer), a transaction
    /// date and a promised delivery date - both dates are plan.md §1 <c>DATE NOT NULL</c> columns
    /// with no server default, so the client must state them.
    /// </summary>
    /// <exception cref="SalesValidationException">An invariant was violated.</exception>
    public static void EnsureValidOrderRequest(Guid customerId, DateOnly? transactionDate, DateOnly? deliveryDate)
    {
        if (customerId == Guid.Empty)
        {
            throw new SalesValidationException(
                SellingErrorCodes.CustomerRequired,
                "A sales order must reference a customer (CustomerId is required).");
        }

        if (transactionDate is null)
        {
            throw new SalesValidationException(
                SellingErrorCodes.TransactionDateRequired,
                "TransactionDate is required (plan.md §1 SalesOrder.TransactionDate is DATE NOT NULL).");
        }

        if (deliveryDate is null)
        {
            throw new SalesValidationException(
                SellingErrorCodes.DeliveryDateRequired,
                "DeliveryDate is required (plan.md §1 SalesOrder.DeliveryDate is DATE NOT NULL).");
        }
    }

    /// <summary>
    /// Creation invariant: the gapless generator must have produced a number before the row is
    /// saved - an empty OrderNumber would corrupt both the sequence parse and the wire contract.
    /// </summary>
    /// <exception cref="SalesValidationException">The number is null, empty or blank.</exception>
    public static void EnsureOrderNumberAssigned(string? orderNumber)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
        {
            throw new SalesValidationException(
                SellingErrorCodes.OrderNumberRequired,
                "A sales order must carry its gapless SO-YYYY-NNNNN number before it is saved.");
        }

        if (orderNumber.Length > MaxOrderNumberLength)
        {
            throw new SalesValidationException(
                SellingErrorCodes.OrderNumberRequired,
                $"OrderNumber must not exceed {MaxOrderNumberLength} characters.");
        }
    }

    /// <summary>A sales document (order or delivery note) must carry at least one line.</summary>
    /// <exception cref="SalesValidationException">The line list is empty.</exception>
    public static void EnsureHasLines(IReadOnlyCollection<object>? lines)
    {
        if (lines is null || lines.Count == 0)
        {
            throw new SalesValidationException(
                SellingErrorCodes.NoLines,
                "A sales document must contain at least one line.");
        }
    }

    /// <summary>
    /// Order line rules (plan.md §1): quantity strictly positive, rate and amount non-negative
    /// (<c>CK_SalesOrderItem_Quantity/Rate/Amount</c>).
    /// </summary>
    /// <exception cref="SalesValidationException">An invariant was violated.</exception>
    public static void EnsureValidOrderLine(decimal quantity, decimal rate, decimal amount)
    {
        if (quantity <= 0)
        {
            throw new SalesValidationException(
                SellingErrorCodes.InvalidQuantity,
                $"Line quantity must be greater than zero (received {quantity:0.####}).");
        }

        if (rate < 0)
        {
            throw new SalesValidationException(
                SellingErrorCodes.InvalidRate,
                $"Line rate must not be negative (received {rate:0.####}).");
        }

        if (amount < 0)
        {
            throw new SalesValidationException(
                SellingErrorCodes.InvalidAmount,
                $"Line amount must not be negative (received {amount:0.####}).");
        }
    }

    /// <summary>
    /// Delivery note line rule (plan.md §1.6 CK_DeliveryNoteLine_Qty): strictly positive quantity.
    /// The SL-04 ceiling itself (<c>Quantity - DeliveredQuantity</c>) is checked against the loaded
    /// order by <see cref="Services.SalesPostingService"/>.
    /// </summary>
    /// <exception cref="SalesValidationException">The quantity is not positive.</exception>
    public static void EnsureValidDeliveryLine(decimal quantity)
    {
        if (quantity <= 0)
        {
            throw new SalesValidationException(
                SellingErrorCodes.InvalidQuantity,
                $"Delivery line quantity must be greater than zero (received {quantity:0.####}).");
        }
    }

    /// <summary>Document totals rule (plan.md §1 CK_SalesOrder_Totals): no negative money column.</summary>
    /// <exception cref="SalesValidationException">A total is negative.</exception>
    public static void EnsureValidTotals(decimal netTotal, decimal taxTotal, decimal grandTotal)
    {
        if (netTotal < 0 || taxTotal < 0 || grandTotal < 0)
        {
            throw new SalesValidationException(
                SellingErrorCodes.InvalidTotals,
                $"Sales order totals must not be negative "
                + $"(NetTotal {netTotal:0.####}, TaxTotal {taxTotal:0.####}, GrandTotal {grandTotal:0.####}).");
        }
    }

    /// <summary>
    /// Invoice line rules (modules 17/18, ERPNext <c>validate_qty</c> parity from
    /// <c>status_updater.py</c>): a return line MUST be negative, a normal line MUST be
    /// positive - both directions are rejected, never silently flipped. Rates stay
    /// non-negative and the amount must equal <c>quantity * rate</c> in sign.
    /// </summary>
    /// <exception cref="SalesValidationException">An invariant was violated.</exception>
    public static void EnsureValidInvoiceLine(decimal quantity, decimal rate, decimal amount, bool isReturn = false)
    {
        if (!isReturn && quantity <= 0)
        {
            throw new SalesValidationException(
                SellingErrorCodes.InvalidQuantity,
                $"Invoice line quantity must be greater than zero (received {quantity:0.####}).");
        }

        if (isReturn && quantity >= 0)
        {
            throw new SalesValidationException(
                SellingErrorCodes.InvalidQuantity,
                $"Return line quantity must be negative (received {quantity:0.####}).");
        }

        if (rate < 0)
        {
            throw new SalesValidationException(
                SellingErrorCodes.InvalidRate,
                $"Line rate must not be negative (received {rate:0.####}).");
        }

        if (!isReturn && amount < 0)
        {
            throw new SalesValidationException(
                SellingErrorCodes.InvalidAmount,
                $"Line amount must not be negative (received {amount:0.####}).");
        }

        if (isReturn && amount > 0)
        {
            throw new SalesValidationException(
                SellingErrorCodes.InvalidAmount,
                $"Return line amount must not be positive (received {amount:0.####}).");
        }
    }

    /// <summary>
    /// Global discount rules (module 17, ERPNext "apply on Net Total" default): percentage in
    /// 0-100, amount non-negative and never above the (absolute) net base. Both zero means
    /// "no discount". When both are supplied they must agree within one cent.
    /// </summary>
    /// <exception cref="SalesValidationException">An invariant was violated.</exception>
    public static void EnsureValidDiscount(decimal discountPercentage, decimal discountAmount, decimal netTotal)
    {
        if (discountPercentage < 0 || discountPercentage > 100)
        {
            throw new SalesValidationException(
                SellingErrorCodes.InvalidDiscount,
                $"Discount percentage must be between 0 and 100 (received {discountPercentage:0.####}).");
        }

        if (discountAmount < 0)
        {
            throw new SalesValidationException(
                SellingErrorCodes.InvalidDiscount,
                $"Discount amount must not be negative (received {discountAmount:0.####}).");
        }

        if (discountAmount > Math.Abs(netTotal) + 0.005m && Math.Abs(netTotal) > 0)
        {
            throw new SalesValidationException(
                SellingErrorCodes.InvalidDiscount,
                $"Discount amount ({discountAmount:0.####}) cannot exceed the net total ({netTotal:0.####}).");
        }

        if (discountPercentage > 0 && discountAmount > 0)
        {
            var expected = Math.Round(Math.Abs(netTotal) * discountPercentage / 100m, 2, MidpointRounding.AwayFromZero);
            if (Math.Abs(expected - discountAmount) > 0.01m)
            {
                throw new SalesValidationException(
                    SellingErrorCodes.InvalidDiscount,
                    $"Discount amount ({discountAmount:0.####}) does not match {discountPercentage:0.####}% "
                    + $"of the net total (expected {expected:0.####}).");
            }
        }
    }

    /// <summary>
    /// Tax row rule (module 17, ERPNext "On Net Total" default): rate in 0-100. The account
    /// itself (postable liability leaf of the company) is resolved by the handler, which owns
    /// data access - this guard covers only the rate shape.
    /// </summary>
    /// <exception cref="SalesValidationException">An invariant was violated.</exception>
    public static void EnsureValidTaxRate(decimal rate)
    {
        if (rate < 0 || rate > 100)
        {
            throw new SalesValidationException(
                SellingErrorCodes.InvalidTaxRate,
                $"Tax rate must be between 0 and 100 (received {rate:0.####}).");
        }
    }

    /// <summary>
    /// Invoice totals sign rule (modules 17/18): normal invoices keep the legacy
    /// non-negative invariant; returns carry all-negative money columns (spec 18 §3).
    /// </summary>
    /// <exception cref="SalesValidationException">A total has the wrong sign.</exception>
    public static void EnsureInvoiceTotalsSign(decimal netTotal, decimal taxTotal, decimal grandTotal, bool isReturn = false)
    {
        if (!isReturn)
        {
            EnsureValidTotals(netTotal, taxTotal, grandTotal);
            return;
        }

        if (netTotal > 0 || taxTotal > 0 || grandTotal > 0)
        {
            throw new SalesValidationException(
                SellingErrorCodes.InvalidTotals,
                $"Return totals must not be positive "
                + $"(NetTotal {netTotal:0.####}, TaxTotal {taxTotal:0.####}, GrandTotal {grandTotal:0.####}).");
        }
    }

    /// <summary>Workflow rule (Task 5.2): only a Draft order can be submitted.</summary>
    /// <exception cref="SalesValidationException">The order is not in Draft.</exception>
    public static void EnsureSubmittable(SalesOrderStatus status)
    {
        if (status != SalesOrderStatus.Draft)
        {
            throw new SalesValidationException(
                SellingErrorCodes.InvalidStatusTransition,
                $"Only a Draft sales order can be submitted; the order is '{status}'.");
        }
    }

    /// <summary>
    /// Workflow rule (Task 5.2b): a delivery note may only reference a confirmed order - Draft has
    /// not passed the credit gate yet and Completed/Cancelled have nothing left to ship. The
    /// per-line SL-04 ceiling is enforced separately by the posting engine.
    /// </summary>
    /// <exception cref="SalesValidationException">The order does not accept deliveries.</exception>
    public static void EnsureDeliverable(SalesOrderStatus status)
    {
        if (status is not (SalesOrderStatus.Submitted or SalesOrderStatus.PartiallyDelivered))
        {
            throw new SalesValidationException(
                SellingErrorCodes.SalesOrderNotDeliverable,
                $"A delivery note requires a Submitted sales order; the order is '{status}'.");
        }
    }
}
