using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Payments.Commands;

/// <summary>
/// Submits a Draft payment voucher (spec R-12): assigns the gapless <c>PAY-YYYY-NNNNN</c>
/// number, revalidates every allocation against the FRESH invoice outstanding (PE-02/PE-07),
/// posts the balanced settlement voucher to the GL (PE-01) and settles the invoices.
/// Carries the original <c>RowVersion</c> for the compare-and-swap.
/// </summary>
public sealed record SubmitPaymentEntryCommand(
    Guid PaymentEntryId,
    Guid CompanyId,
    byte[]? RowVersion) : ICommand<Result<PaymentEntryDto>>;
