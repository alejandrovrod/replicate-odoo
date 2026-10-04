using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Buying.Commands;

/// <summary>
/// Optional body of the invoice cancel/status endpoint: only the optimistic-concurrency token.
/// </summary>
public sealed record PurchaseInvoiceStatusRequest(byte[]? RowVersion = null);

/// <summary>
/// Cancels one posted purchase invoice (Task 4.6 / spec BY-05): the bill moves
/// <c>Unpaid</c>/<c>PartiallyPaid</c> -&gt; <c>Cancelled</c> and a compensating row is appended for
/// every original GL line with Debit and Credit SWAPPED - same VoucherId/VoucherNo, original
/// PostingDate, <c>IsCancelled = true</c>, so the voucher nets to exactly 0.0000 and Accounts
/// Payable is restored. The original rows are NEVER mutated or deleted (Constitution III.2), and
/// <see cref="PurchaseInvoiceDto.OutstandingAmount"/> drops to 0.
/// </summary>
/// <param name="CompanyId">Company that owns the invoice (route-independent cross-check).</param>
/// <param name="InvoiceId">Purchase invoice id from the route.</param>
/// <param name="RowVersion">Optional optimistic token - see <see cref="PurchaseInvoiceStatusRequest"/>.</param>
public sealed record CancelPurchaseInvoiceCommand(
    Guid CompanyId,
    Guid InvoiceId,
    byte[]? RowVersion = null) : ICommand<Result<PurchaseInvoiceDto>>;
