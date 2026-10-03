using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.GeneralLedger.Commands;

/// <summary>
/// Cancels one Submitted journal entry (spec AC-07 / Constitution III.3): the header moves to
/// <c>Cancelled</c> AND a compensating row is appended for every original line with Debit and
/// Credit SWAPPED - same VoucherId/VoucherNo, original PostingDate, <c>IsCancelled = true</c>.
/// The originals are NEVER mutated or deleted (Constitution III.2), and afterwards the voucher's
/// net balance is exactly 0.0000 (trial-balance-neutral).
/// </summary>
/// <param name="CompanyId">Company that owns the voucher (route-independent cross-check).</param>
/// <param name="JournalEntryId">Voucher id from the route.</param>
/// <param name="RowVersion">Optional optimistic token - see <see cref="JournalEntryStatusRequest"/>.</param>
public sealed record CancelJournalEntryCommand(
    Guid CompanyId,
    Guid JournalEntryId,
    byte[]? RowVersion = null) : ICommand<Result<JournalEntryDto>>;
