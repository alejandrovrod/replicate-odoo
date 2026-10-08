using System;
using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.GeneralLedger.PeriodClosing;

/// <summary>
/// Cancels one Submitted closing voucher (R-13): appends the compensating reversal, never mutates.
/// Carries the optimistic token plus the idempotency key.
/// </summary>
public record CancelPeriodClosingVoucherCommand(
    Guid VoucherId,
    Guid CompanyId,
    byte[]? RowVersion = null,
    string? IdempotencyKey = null) : ICommand<Result<PeriodClosingVoucherDto>>;
