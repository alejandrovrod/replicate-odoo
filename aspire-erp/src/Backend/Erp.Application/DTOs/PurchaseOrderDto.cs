using Erp.Domain.Entities;

namespace Erp.Application.DTOs;

/// <summary>One line of a purchase order as returned by the API.</summary>
public sealed record PurchaseOrderLineDto(
    Guid ItemId,
    string ItemCode,
    string ItemName,
    decimal Qty,
    decimal Rate,
    int LineNumber);

/// <summary>Purchase order payload (Task 4.1): header + lines + supplier identity for the list view.</summary>
public sealed record PurchaseOrderDto(
    Guid Id,
    Guid CompanyId,
    Guid SupplierId,
    string SupplierCode,
    string SupplierName,
    PurchaseOrderStatus Status,
    DateOnly PostingDate,
    string VoucherNo,
    DateTimeOffset CreatedAt,
    IReadOnlyList<PurchaseOrderLineDto> Lines)
{
    /// <summary>Maps an order aggregate; <paramref name="itemsById"/> resolves line codes/names.</summary>
    public static PurchaseOrderDto Build(
        PurchaseOrder order,
        Supplier supplier,
        IReadOnlyDictionary<Guid, Item> itemsById)
    {
        var lines = new List<PurchaseOrderLineDto>(order.Lines.Count);
        foreach (var line in order.Lines.OrderBy(l => l.LineNumber))
        {
            itemsById.TryGetValue(line.ItemId, out var item);
            lines.Add(new PurchaseOrderLineDto(
                line.ItemId,
                item?.Code ?? line.ItemId.ToString(),
                item?.Name ?? string.Empty,
                line.Qty,
                line.Rate,
                line.LineNumber));
        }

        return new PurchaseOrderDto(
            order.Id,
            order.CompanyId,
            order.SupplierId,
            supplier.Code,
            supplier.Name,
            order.Status,
            order.PostingDate,
            order.VoucherNo,
            order.CreatedAt,
            lines);
    }
}
