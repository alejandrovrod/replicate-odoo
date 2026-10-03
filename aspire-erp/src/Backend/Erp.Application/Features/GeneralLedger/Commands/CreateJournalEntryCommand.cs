using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;

namespace Erp.Application.Features.GeneralLedger.Commands;

/// <summary>One line of a <see cref="CreateJournalEntryCommand"/>.</summary>
/// <param name="AccountId">Target posting account (leaf - checked at submit, spec AC-03).</param>
/// <param name="Debit">Debit amount (decimal(18,4), >= 0; zero + zero on a line is rejected).</param>
/// <param name="Credit">Credit amount (decimal(18,4), >= 0; zero + zero on a line is rejected).</param>
/// <param name="PartyType">Counterparty role, e.g. "Supplier" (mirrored onto GLEntry.PartyType).</param>
/// <param name="PartyId">Primary key of the party row referenced by <paramref name="PartyType"/>.</param>
/// <param name="CostCenterId">Cost Center dimension (mirrored onto GLEntry.CostCenterId).</param>
public sealed record CreateJournalEntryLine(
    Guid AccountId,
    decimal Debit,
    decimal Credit,
    string? PartyType = null,
    Guid? PartyId = null,
    Guid? CostCenterId = null);

/// <summary>
/// Creates one Journal Entry in Draft with its gapless JV-YYYY-NNNNN voucher (Constitution III.4)
/// - step ONE of the two-step workflow documented on <see cref="Domain.Entities.JournalEntry"/>:
/// only the header and its JournalEntryLine rows are persisted, NO GLEntry row exists yet.
/// </summary>
/// <remarks>
/// Deliberate mirror of plan.md §3's <c>SubmitJournalEntryCommand(CompanyId, PostingDate,
/// VoucherType, UserRemark, Lines)</c> payload: the fields are the same, the effect is split -
/// see the aggregate remarks for why (tasks.md 2.4 needs a distinct /submit action, and spec
/// AC-02/AC-03 are phrased as "submit this draft and reject it").
/// Balance, freeze and group-account validation are NOT enforced here: an imbalanced draft is
/// exactly what spec AC-02 starts from ("Given a draft Journal Entry with total debits $1,000.00
/// and total credits $995.00"). They all run on submit.
/// </remarks>
/// <param name="CompanyId">Company that owns the voucher.</param>
/// <param name="PostingDate">Accounting date; defaults to today (UTC) - it also seeds the JV sequence year.</param>
/// <param name="Type">Economic nature of the voucher (defaults to <c>Standard</c>).</param>
/// <param name="UserRemark">Free-text remark copied to GLEntry.Remarks on submit.</param>
/// <param name="Lines">The voucher's debit/credit lines (at least one).</param>
public sealed record CreateJournalEntryCommand(
    Guid CompanyId,
    DateOnly? PostingDate = null,
    JournalEntryType Type = JournalEntryType.Standard,
    string? UserRemark = null,
    IReadOnlyList<CreateJournalEntryLine>? Lines = null) : ICommand<Result<JournalEntryDto>>;
