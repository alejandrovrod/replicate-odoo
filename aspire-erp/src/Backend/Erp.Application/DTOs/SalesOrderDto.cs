using Erp.Domain.Entities;

namespace Erp.Application.DTOs;

/// <summary>
/// One line of a sales order as returned by the API: the ordered quantity plus the two counters
/// Task 5.2 tracks (delivery progress written by the DeliveryNote, billing progress by Task 5.3).
/// </summary>
public sealed record SalesOrderLineDto(
    Guid Id,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    decimal Quantity,
    decimal DeliveredQuantity,
    decimal BilledQuantity,
    decimal Rate,
    decimal Amount,
    decimal DeliveredPercentage,
    decimal BilledPercentage);

/// <summary>Sales order payload (Task 5.2): header + lines + customer identity for the list view.</summary>
public sealed record SalesOrderDto(
    Guid Id,
    Guid CompanyId,
    Guid CustomerId,
    string CustomerCode,
    string CustomerName,
    SalesOrderStatus Status,
    DateOnly TransactionDate,
    DateOnly DeliveryDate,
    string OrderNumber,
    decimal NetTotal,
    decimal TaxTotal,
    decimal GrandTotal,
    decimal DeliveredPercentage,
    decimal BilledPercentage,
    DateTimeOffset CreatedAt,
    IReadOnlyList<SalesOrderLineDto> Lines)
{
    /// <summary>
    /// Maps an order aggregate; <paramref name="itemsById"/> resolves line codes/names and
    /// <paramref name="customer"/> the customer identity (null only if the FK row vanished, which
    /// Restrict makes impossible - the fallback keeps the read defensive like the buying mapper).
    /// </summary>
    public static SalesOrderDto Build(
        SalesOrder order,
        Customer? customer,
        IReadOnlyDictionary<Guid, Item> itemsById)
    {
        var lines = new List<SalesOrderLineDto>(order.Lines.Count);
        foreach (var line in order.Lines)
        {
            itemsById.TryGetValue(line.ItemId, out var item);
            lines.Add(new SalesOrderLineDto(
                line.Id,
                line.ItemId,
                item?.ItemCode ?? line.ItemId.ToString(),
                item?.ItemName ?? string.Empty,
                line.Quantity,
                line.DeliveredQuantity,
                line.BilledQuantity,
                line.Rate,
                line.Amount,
                Percentage(line.DeliveredQuantity, line.Quantity),
                Percentage(line.BilledQuantity, line.Quantity)));
        }

        return new SalesOrderDto(
            order.Id,
            order.CompanyId,
            order.CustomerId,
            customer?.CustomerCode ?? string.Empty,
            customer?.CustomerName ?? string.Empty,
            order.Status,
            order.TransactionDate,
            order.DeliveryDate,
            order.OrderNumber,
            order.NetTotal,
            order.TaxTotal,
            order.GrandTotal,
            order.DeliveredPercentage,
            order.BilledPercentage,
            order.CreatedAt,
            lines);
    }

    /// <summary>
    /// Line-level fulfillment ratio (2 decimals, AwayFromZero) - the same quantity-weighted
    /// formula the header carries, applied to a single line: counter / ordered quantity * 100.
    /// </summary>
    private static decimal Percentage(decimal counter, decimal quantity) =>
        quantity <= 0
            ? 0m
            : Math.Round(counter / quantity * 100m, 2, MidpointRounding.AwayFromZero);
}
