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
