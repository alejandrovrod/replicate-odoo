using Erp.Domain.Entities;

namespace Erp.Application.DTOs;

/// <summary>One item of a purchase order as returned by the API.</summary>
public sealed record PurchaseOrderItemDto(
    Guid Id,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    decimal Quantity,
    decimal ReceivedQuantity,
    decimal BilledQuantity,
    decimal Rate,
    decimal Amount,
    int LineNumber);

/// <summary>Purchase order payload (Task 4.1): header + lines + supplier identity for the list view.</summary>
public sealed record PurchaseOrderDto(
    Guid Id,
    Guid CompanyId,
    Guid SupplierId,
    string SupplierCode,
    string SupplierName,
    PurchaseOrderStatus Status,
    DateOnly TransactionDate,
    DateOnly ScheduleDate,
    string OrderNumber,
    decimal NetTotal,
    decimal TaxTotal,
    decimal GrandTotal,
    decimal ReceivedPercentage,
    decimal BilledPercentage,
    DateTimeOffset CreatedAt,
    IReadOnlyList<PurchaseOrderItemDto> Items)
{
    /// <summary>Maps an order aggregate; <paramref name="itemsById"/> resolves line codes/names.</summary>
    public static PurchaseOrderDto Build(
        PurchaseOrder order,
        Supplier supplier,
        IReadOnlyDictionary<Guid, Item> itemsById)
    {
        var items = new List<PurchaseOrderItemDto>(order.Items.Count);
        foreach (var item in order.Items.OrderBy(i => i.LineNumber))
        {
            itemsById.TryGetValue(item.ItemId, out var itemMaster);
            items.Add(new PurchaseOrderItemDto(
                item.Id,
                item.ItemId,
                itemMaster?.ItemCode ?? item.ItemId.ToString(),
                itemMaster?.ItemName ?? string.Empty,
                item.Quantity,
                item.ReceivedQuantity,
                item.BilledQuantity,
                item.Rate,
                item.Amount,
                item.LineNumber));
        }

        return new PurchaseOrderDto(
            order.Id,
            order.CompanyId,
            order.SupplierId,
            supplier.Code,
            supplier.Name,
            order.Status,
            order.TransactionDate,
            order.ScheduleDate,
            order.OrderNumber,
            order.NetTotal,
            order.TaxTotal,
            order.GrandTotal,
            order.ReceivedPercentage,
            order.BilledPercentage,
            order.CreatedAt,
            items);
    }
}
