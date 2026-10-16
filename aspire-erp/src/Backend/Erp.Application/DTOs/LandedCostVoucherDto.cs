using Erp.Domain.Entities;

namespace Erp.Application.DTOs;

/// <summary>One landed cost charge as stored (Fase 4).</summary>
public sealed record LandedCostChargeDto(
    Guid Id,
    string Description,
    decimal Amount,
    Guid ExpenseAccountId,
    string ExpenseAccountCode,
    string ExpenseAccountName);

/// <summary>One apportioned receipt line as stored (Fase 4).</summary>
public sealed record LandedCostApportionmentDto(
    Guid PurchaseReceiptId,
    string PurchaseReceiptNo,
    Guid PurchaseReceiptLineId,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    decimal Qty,
    decimal OriginalRate,
    decimal ApplicableCharges,
    decimal NewRate);

/// <summary>Landed cost voucher payload (Fase 4).</summary>
public sealed record LandedCostVoucherDto(
    Guid Id,
    Guid CompanyId,
    string VoucherNo,
    DateOnly PostingDate,
    DocumentStatus Status,
    LandedCostDistributeBy DistributeBy,
    decimal TotalTaxesAndCharges,
    byte[]? RowVersion,
    DateTimeOffset CreatedAt,
    IReadOnlyList<Guid> ReceiptIds,
    IReadOnlyList<LandedCostChargeDto> Charges,
    IReadOnlyList<LandedCostApportionmentDto> Items)
{
    public static LandedCostVoucherDto Build(
        LandedCostVoucher voucher,
        IReadOnlyDictionary<Guid, Item> itemsById,
        IReadOnlyDictionary<Guid, string> receiptNumbers)
    {
        return new LandedCostVoucherDto(
            voucher.Id,
            voucher.CompanyId,
            voucher.VoucherNo,
            voucher.PostingDate,
            voucher.Status,
            voucher.DistributeBy,
            voucher.TotalTaxesAndCharges,
            voucher.RowVersion,
            voucher.CreatedAt,
            voucher.Receipts.Select(r => r.PurchaseReceiptId).ToList(),
            voucher.Charges
                .Select(c => new LandedCostChargeDto(
                    c.Id,
                    c.Description,
                    c.Amount,
                    c.ExpenseAccountId,
                    c.ExpenseAccount?.AccountCode ?? string.Empty,
                    c.ExpenseAccount?.AccountName ?? string.Empty))
                .ToList(),
            voucher.Items
                .Select(l =>
                {
                    itemsById.TryGetValue(l.ItemId, out var item);
                    receiptNumbers.TryGetValue(l.PurchaseReceiptId, out var receiptNo);
                    return new LandedCostApportionmentDto(
                        l.PurchaseReceiptId,
                        receiptNo ?? l.PurchaseReceiptId.ToString(),
                        l.PurchaseReceiptLineId,
                        l.ItemId,
                        item?.ItemCode ?? l.ItemId.ToString(),
                        item?.ItemName ?? string.Empty,
                        l.Qty,
                        l.OriginalRate,
                        l.ApplicableCharges,
                        l.NewRate);
                })
                .ToList());
    }
}
