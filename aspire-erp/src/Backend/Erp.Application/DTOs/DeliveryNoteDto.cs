using Erp.Domain.Entities;

namespace Erp.Application.DTOs;

/// <summary>One line of a delivery note as returned by the API (no money: the stock value comes from the FIFO layers).</summary>
public sealed record DeliveryNoteLineDto(
    Guid Id,
    Guid SalesOrderItemId,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    decimal Qty);

/// <summary>Delivery note payload returned by GET /api/v1/deliverynotes.</summary>
public sealed record DeliveryNoteDto(
    Guid Id,
    Guid CompanyId,
    Guid SalesOrderId,
    Guid WarehouseId,
    DateOnly PostingDate,
    string VoucherNo,
    DateTimeOffset CreatedAt,
    IReadOnlyList<DeliveryNoteLineDto> Lines)
{
    /// <summary>Maps a delivery note aggregate; <paramref name="itemsById"/> resolves line codes/names.</summary>
    public static DeliveryNoteDto Build(
        DeliveryNote deliveryNote,
        IReadOnlyDictionary<Guid, Item> itemsById)
    {
        var lines = new List<DeliveryNoteLineDto>(deliveryNote.Lines.Count);
        foreach (var line in deliveryNote.Lines)
        {
            itemsById.TryGetValue(line.ItemId, out var item);
            lines.Add(new DeliveryNoteLineDto(
                line.Id,
                line.SalesOrderItemId,
                line.ItemId,
                item?.ItemCode ?? line.ItemId.ToString(),
                item?.ItemName ?? string.Empty,
                line.Qty));
        }

        return new DeliveryNoteDto(
            deliveryNote.Id,
            deliveryNote.CompanyId,
            deliveryNote.SalesOrderId,
            deliveryNote.WarehouseId,
            deliveryNote.PostingDate,
            deliveryNote.VoucherNo,
            deliveryNote.CreatedAt,
            lines);
    }
}

/// <summary>
/// Full result of POST /api/v1/deliverynotes: the note, its Kardex rows, its balanced General
/// Ledger lines (Dr Cost of Goods Sold / Cr warehouse stock account) and the sales order as it
/// stands AFTER the delivery - everything needed to prove Task 5.2b in the API response itself.
/// </summary>
public sealed record DeliveryNotePostingDto(
    DeliveryNoteDto DeliveryNote,
    IReadOnlyList<StockLedgerEntryDto> LedgerEntries,
    IReadOnlyList<GLEntryDto> GlEntries,
    decimal TotalDebit,
    decimal TotalCredit,
    SalesOrderDto SalesOrder);
