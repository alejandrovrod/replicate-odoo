using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Services;

namespace Erp.Application.Features.Buying.Commands;

/// <summary>One line of a <see cref="PostPurchaseInvoiceCommand"/>: bills one receipt line in full.</summary>
public sealed record PostPurchaseInvoiceLine(
    Guid PurchaseReceiptLineId,
    Guid ItemId,
    decimal Qty,
    decimal Rate);

/// <summary>
/// Creates AND posts one purchase invoice (Task 4.3 / spec BY-01) in a single transaction: clears
/// the interim liability at receipt value, books Input Tax and the gross Accounts Payable, the
/// gapless PINV voucher and the linked order's -&gt; Billed transition. There is no draft state:
/// the command IS the posting.
/// </summary>
public sealed record PostPurchaseInvoiceCommand(
    Guid CompanyId,
    Guid PurchaseReceiptId,
    DateOnly? PostingDate = null,
    decimal TaxAmount = 0m,
    IReadOnlyList<PostPurchaseInvoiceLine>? Lines = null) : ICommand<Result<PurchaseInvoicePostingDto>>;
