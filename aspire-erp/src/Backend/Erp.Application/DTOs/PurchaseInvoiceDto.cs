using Erp.Domain.Entities;

namespace Erp.Application.DTOs;

/// <summary>One line of a purchase invoice as returned by the API (bills one receipt line in full).</summary>
public sealed record PurchaseInvoiceLineDto(
    Guid PurchaseReceiptLineId,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    decimal Qty,
    decimal Rate,
    int LineNumber);

/// <summary>Purchase invoice payload returned by GET /api/v1/purchaseinvoices.</summary>
public sealed record PurchaseInvoiceDto(
    Guid Id,
    Guid CompanyId,
    Guid PurchaseReceiptId,
    DateOnly PostingDate,
    decimal TaxAmount,
    string VoucherNo,
    DateTimeOffset CreatedAt,
    IReadOnlyList<PurchaseInvoiceLineDto> Lines)
{
    /// <summary>Maps an invoice aggregate; <paramref name="itemsById"/> resolves line codes/names.</summary>
    public static PurchaseInvoiceDto Build(
        PurchaseInvoice invoice,
        IReadOnlyDictionary<Guid, Item> itemsById)
    {
        var lines = new List<PurchaseInvoiceLineDto>(invoice.Lines.Count);
        foreach (var line in invoice.Lines.OrderBy(l => l.LineNumber))
        {
            itemsById.TryGetValue(line.ItemId, out var item);
            lines.Add(new PurchaseInvoiceLineDto(
                line.PurchaseReceiptLineId,
                line.ItemId,
                item?.Code ?? line.ItemId.ToString(),
                item?.Name ?? string.Empty,
                line.Qty,
                line.Rate,
                line.LineNumber));
        }

        return new PurchaseInvoiceDto(
            invoice.Id,
            invoice.CompanyId,
            invoice.PurchaseReceiptId,
            invoice.PostingDate,
            invoice.TaxAmount,
            invoice.VoucherNo,
            invoice.CreatedAt,
            lines);
    }
}

/// <summary>
/// Full result of POST /api/v1/purchaseinvoices: the invoice, its balanced General Ledger lines
/// (interim liability clearance + Input Tax + Accounts Payable, spec BY-01) - everything needed to
/// prove Task 4.3 in the API response itself. <see cref="OrderStatus"/> is the linked order's
/// workflow state after billing (when the receipt fulfills an order).
/// </summary>
public sealed record PurchaseInvoicePostingDto(
    PurchaseInvoiceDto Invoice,
    IReadOnlyList<GLEntryDto> GlEntries,
    decimal TotalDebit,
    decimal TotalCredit,
    PurchaseOrderStatus? OrderStatus);
