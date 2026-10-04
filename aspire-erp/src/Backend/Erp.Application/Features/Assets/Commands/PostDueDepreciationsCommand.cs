using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Assets.Commands;

/// <summary>
/// Posts every due depreciation line of a company (Task 10.4, scenarios AS-02/AS-04): the
/// schedulable unit of the background worker, expressed as an idempotent command. Loads all
/// schedule lines with ScheduleDate &lt;= <see cref="AsOfDate"/> on the company's assets,
/// books the Scheduled ones held by Capitalized assets (Dr Expense / Cr Accumulated
/// Depreciation, spec AS-02) into ONE gapless batch voucher, and skips every other row with
/// its identity and reason instead of failing the batch (spec AS-04 replay safety).
/// </summary>
/// <remarks>
/// No hosted service ships in this block: the command IS the schedulable unit; the host (or an
/// operator POST) invokes it with an <c>Idempotency-Key</c> per run. Full 10.6 replay wiring
/// (idempotency filter + reversal) stays Block C.
/// </remarks>
public sealed record PostDueDepreciationsCommand(
    Guid CompanyId,
    DateOnly AsOfDate) : ICommand<Result<DepreciationRunDto>>;
