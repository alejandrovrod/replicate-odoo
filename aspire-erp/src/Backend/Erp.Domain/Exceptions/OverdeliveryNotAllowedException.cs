using Erp.Domain.Entities;

namespace Erp.Domain.Exceptions;

/// <summary>
/// Invariant SL-04 (Non-Overdelivery Guard): a <c>DeliveryNote</c> may not fulfil more than the
/// remaining unfulfilled quantity of the referenced order line,
/// <c>SalesOrderItem.Quantity - SalesOrderItem.DeliveredQuantity</c>. Thrown by
/// <see cref="Services.SalesPostingService"/> BEFORE any FIFO layer is consumed, so a rejected
/// attempt writes zero rows.
/// </summary>
/// <remarks>
/// Carries the item, the order number, what is left and what was asked so the RFC 7807
/// <c>detail</c> tells the operator exactly which line overflowed. The <see cref="Code"/> travels
/// Domain -&gt; Application (<c>Result.Failure</c>) -&gt; Api, where the controller maps it to
/// HTTP 400 (a quantity that does not fit the order describes a bad REQUEST - same family as
/// the buying module's <c>quantity_mismatch</c>).
/// </remarks>
public sealed class OverdeliveryNotAllowedException : Exception
{
    /// <summary>Stable failure code carried into the RFC 7807 <c>code</c> extension.</summary>
    public string Code { get; } = SellingErrorCodes.OverdeliveryNotAllowed;

    /// <summary>Item that overflowed (named in the message for operators).</summary>
    public string ItemCode { get; }

    /// <summary>Order number whose line refused the shipment.</summary>
    public string OrderNumber { get; }

    /// <summary>Units still deliverable on that line at the time of the attempt.</summary>
    public decimal Remaining { get; }

    /// <summary>Units this attempt tried to deliver.</summary>
    public decimal Requested { get; }

    public OverdeliveryNotAllowedException(
        string itemCode,
        string orderNumber,
        decimal remaining,
        decimal requested)
        : base(
            $"Overdelivery is not allowed for item '{itemCode}' on sales order '{orderNumber}' "
            + $"(spec SL-04): remaining {remaining:0.####}, requested {requested:0.####}.")
    {
        ItemCode = itemCode;
        OrderNumber = orderNumber;
        Remaining = remaining;
        Requested = requested;
    }
}
