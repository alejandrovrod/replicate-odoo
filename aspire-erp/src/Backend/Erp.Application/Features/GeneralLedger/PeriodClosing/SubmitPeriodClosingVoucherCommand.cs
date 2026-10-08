using System;
using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.GeneralLedger.PeriodClosing;

/// <summary>
/// Submits one Draft closing voucher (R-13, plan.md §4): computes the FY-windowed P&amp;L,
/// validates the retained leaf, posts the balanced close and flips to Submitted — all inside ONE
/// serializable transaction. Carries the optimistic token plus the idempotency key.
/// </summary>
public record SubmitPeriodClosingVoucherCommand(
    Guid VoucherId,
    Guid CompanyId,
    byte[]? RowVersion = null,
    string? IdempotencyKey = null) : ICommand<Result<PeriodClosingVoucherDto>>;
