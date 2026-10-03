using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.GeneralLedger.Commands;

/// <summary>
/// Optional body of the status-transition endpoints (<c>/submit</c> and <c>/cancel</c>): it
/// carries ONLY the optimistic concurrency token, so a client that wants a compare-and-swap
/// transition echoes the <c>rowVersion</c> it read from <see cref="JournalEntryDto"/> and a stale
/// value fails with <c>concurrency_conflict</c> (409).
/// </summary>
/// <remarks>
/// The body itself is optional (the endpoints accept an empty body):
/// <list type="bullet">
/// <item>Omitted/null - the SERVER-side token still protects the load/save race, which is exactly
/// how the buying submit endpoint behaves today (discovery note: no existing command takes a
/// client RowVersion - neither header nor body - so this field is additive, never required);</item>
/// <item>Present - the token is compared against the loaded row before any validation, so a stale
/// client observes the conflict instead of a confusing domain error.</item>
/// </list>
/// </remarks>
public sealed record JournalEntryStatusRequest(byte[]? RowVersion = null);

/// <summary>
/// Advances one Draft journal entry to Submitted - step TWO of the two-step workflow: this is
/// where plan.md §3's canonical validation runs (balance -&gt; freeze -&gt; group account) and where
/// N balanced rows are appended to GLEntry, all inside ONE transaction (spec AC-01/AC-02/AC-03/AC-04;
/// tasks.md 2.4 "appends ledger rows atomically").
/// </summary>
/// <param name="CompanyId">Company that owns the voucher (route-independent cross-check).</param>
/// <param name="JournalEntryId">Voucher id from the route.</param>
/// <param name="RowVersion">Optional optimistic token - see <see cref="JournalEntryStatusRequest"/>.</param>
public sealed record SubmitJournalEntryCommand(
    Guid CompanyId,
    Guid JournalEntryId,
    byte[]? RowVersion = null) : ICommand<Result<JournalEntryDto>>;
