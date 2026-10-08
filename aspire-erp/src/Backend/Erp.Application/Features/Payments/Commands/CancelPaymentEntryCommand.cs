using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Payments.Commands;

/// <summary>
/// Cancels a Submitted payment voucher (spec R-12 PE-05): appends the compensating GL reversal
/// and restores every settled invoice. Carries the original <c>RowVersion</c> for the
/// compare-and-swap. Reconciled vouchers (BN-06) are rejected until un-reconciled.
/// </summary>
public sealed record CancelPaymentEntryCommand(
    Guid PaymentEntryId,
    Guid CompanyId,
    byte[]? RowVersion) : ICommand<Result<PaymentEntryDto>>;
