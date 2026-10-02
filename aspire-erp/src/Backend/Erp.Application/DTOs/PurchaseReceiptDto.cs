using Erp.Domain.Entities;

namespace Erp.Application.DTOs;

/// <summary>
/// One line of a purchase receipt as returned by the API. <see cref="Id"/> is the anchor invoice
/// lines bill against (three-way matching, Task 4.3).
/// </summary>
public sealed record PurchaseReceiptLineDto(
    Guid Id,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    decimal Qty,
    decimal Rate,
    int LineNumber);

/// <summary>Purchase receipt payload returned by GET /api/v1/purchasereceipts.</summary>
public sealed record PurchaseReceiptDto(
    Guid Id,
    Guid CompanyId,
    Guid? PurchaseOrderId,
    Guid WarehouseId,
    DateOnly PostingDate,
    string VoucherNo,
    DateTimeOffset CreatedAt,
    IReadOnlyList<PurchaseReceiptLineDto> Lines)
{
    /// <summary>Maps a receipt aggregate; <paramref name="itemsById"/> resolves line codes/names.</summary>
    public static PurchaseReceiptDto Build(
        PurchaseReceipt receipt,
        IReadOnlyDictionary<Guid, Item> itemsById)
    {
        var lines = new List<PurchaseReceiptLineDto>(receipt.Lines.Count);
        foreach (var line in receipt.Lines.OrderBy(l => l.LineNumber))
        {
            itemsById.TryGetValue(line.ItemId, out var item);
            lines.Add(new PurchaseReceiptLineDto(
                line.Id,
                line.ItemId,
                item?.Code ?? line.ItemId.ToString(),
                item?.Name ?? string.Empty,
                line.Qty,
                line.Rate,
                line.LineNumber));
        }

        return new PurchaseReceiptDto(
            receipt.Id,
            receipt.CompanyId,
            receipt.PurchaseOrderId,
            receipt.WarehouseId,
            receipt.PostingDate,
            receipt.VoucherNo,
            receipt.CreatedAt,
            lines);
    }
}

/// <summary>
/// Full result of POST /api/v1/purchasereceipts: the receipt, its Kardex rows and its balanced
/// General Ledger lines - everything needed to prove Task 4.2 in the API response itself.
/// <see cref="OrderStatus"/> is the linked order's workflow state after the receipt (when ordered).
/// </summary>
public sealed record PurchaseReceiptPostingDto(
    PurchaseReceiptDto Receipt,
    IReadOnlyList<StockLedgerEntryDto> LedgerEntries,
    IReadOnlyList<GLEntryDto> GlEntries,
    decimal TotalDebit,
    decimal TotalCredit,
    PurchaseOrderStatus? OrderStatus);
