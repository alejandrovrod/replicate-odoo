using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Services;

namespace Erp.Application.Features.Buying.Commands;

/// <summary>
/// One line of a <see cref="PostPurchaseReturnCommand"/>: the ORIGINAL invoice line coming back and
/// the strictly positive quantity being returned. Provenance (item, rate, receipt line) is copied
/// from the original line by the posting engine.
/// </summary>
public sealed record PostPurchaseReturnLine(
    Guid PurchaseInvoiceLineId,
    decimal Qty);

/// <summary>
/// Creates AND posts one supplier debit note (purchase return, module 19 / spec
/// returns-and-credit-notes T1) against a posted bill, in a single transaction: the strict inverse
/// of <see cref="PostPurchaseInvoiceCommand"/> - Debit Accounts Payable / Credit Input Tax / Credit
/// inventory (with negative Kardex rows when <c>UpdateStock</c>) or Credit the accrued liability for
/// an amount-only adjustment - a gapless PRET voucher and the amortization of the referenced bill's
/// outstanding balance. There is no draft state: the command IS the posting.
/// </summary>
public sealed record PostPurchaseReturnCommand(
    Guid CompanyId,
    Guid ReturnAgainstId,
    string BillNumber = "",
    Guid? CurrencyId = null,
    DateOnly? DueDate = null,
    DateOnly? PostingDate = null,
    decimal TaxAmount = 0m,
    bool UpdateStock = false,
    IReadOnlyList<PostPurchaseReturnLine>? Lines = null) : ICommand<Result<PurchaseInvoicePostingDto>>;

/// <summary>
/// Request body of <c>POST /api/v1/purchaseinvoices/{id}/return</c>: the company and the reversed
/// bill travel on the route, so the body only carries the return itself.
/// </summary>
public sealed record PurchaseReturnRequest(
    IReadOnlyList<PostPurchaseReturnLine>? Lines = null,
    string? BillNumber = null,
    decimal TaxAmount = 0m,
    bool UpdateStock = false,
    DateOnly? PostingDate = null,
    DateOnly? DueDate = null,
    Guid? CurrencyId = null);
