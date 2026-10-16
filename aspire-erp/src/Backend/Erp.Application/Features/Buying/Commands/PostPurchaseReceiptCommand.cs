using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Services;

namespace Erp.Application.Features.Buying.Commands;

/// <summary>One line of a <see cref="PostPurchaseReceiptCommand"/>.</summary>
public sealed record PostPurchaseReceiptLine(
    Guid ItemId,
    decimal Qty,
    decimal Rate,
    // Batch-and-serial Fase 3: receipt batch number (+ master data when creating) and the
    // exact serials received when the item is serial-tracked.
    string? BatchNo = null,
    DateOnly? ManufacturingDate = null,
    DateOnly? ExpiryDate = null,
    IReadOnlyList<string>? SerialNumbers = null);

/// <summary>
/// Creates AND posts one purchase receipt (Task 4.2) in a single transaction: Kardex rows,
/// balanced General Ledger lines (Dr stock account / Cr Stock Received But Not Billed), the
/// gapless PR voucher and the linked order's -&gt; Received transition. There is no draft state:
/// the command IS the posting.
/// </summary>
public sealed record PostPurchaseReceiptCommand(
    Guid CompanyId,
    Guid WarehouseId,
    Guid SupplierId,
    Guid? PurchaseOrderId = null,
    DateOnly? PostingDate = null,
    IReadOnlyList<PostPurchaseReceiptLine>? Lines = null) : ICommand<Result<PurchaseReceiptPostingDto>>;
