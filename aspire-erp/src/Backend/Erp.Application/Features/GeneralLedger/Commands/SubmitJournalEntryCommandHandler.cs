using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using Erp.Domain.Services;

namespace Erp.Application.Features.GeneralLedger.Commands;

/// <summary>
/// Executes <see cref="SubmitJournalEntryCommand"/>: Draft -&gt; Submitted plus the GLEntry append,
/// inside ONE transaction (tasks.md 2.4 "appends ledger rows atomically"). Validation order is
/// deliberate - every gate runs BEFORE the first row is built, so each rejection leaves ZERO
/// ledger rows (spec AC-02/AC-04):
/// <list type="number">
/// <item>existence + company ownership (<c>journal_entry_not_found</c>);</item>
/// <item>optional client RowVersion (<c>concurrency_conflict</c>);</item>
/// <item>plan.md §3 freeze check via <c>Company.EnsurePostingDateUnlocked</c> - first data gate,
/// exactly like the stock and buying posting engines (spec AC-04);</item>
/// <item>plan.md §3 canonical balance <c>|debits - credits| &lt;= 0.0001</c> (spec AC-02);</item>
/// <item>plan.md §3 account resolution - leaf/active/company, group -&gt;
/// <c>InvalidPostingAccountException</c> (spec AC-03);</item>
/// <item>the aggregate state machine <c>JournalEntry.Submit()</c> (invalid move -&gt;
/// <c>invalid_status_transition</c>);</item>
/// <item><c>DoubleEntryGuard</c> over the BUILT rows before SaveChanges (Constitution III.1 -
/// the same defence in depth the other posting engines use).</item>
/// </list>
/// </summary>
public sealed class SubmitJournalEntryCommandHandler
    : ICommandHandler<SubmitJournalEntryCommand, Result<JournalEntryDto>>
{
    private readonly ICompanyRepository _companies;
    private readonly IAccountRepository _accounts;
    private readonly IJournalRepository _journals;

    public SubmitJournalEntryCommandHandler(
        ICompanyRepository companies,
        IAccountRepository accounts,
        IJournalRepository journals)
    {
        _companies = companies;
        _accounts = accounts;
        _journals = journals;
    }

    public async Task<Result<JournalEntryDto>> HandleAsync(
        SubmitJournalEntryCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // One transaction for validation + status + rows: a rollback consumes nothing and
            // writes nothing (spec AC-02 "zero records are written to GLEntry").
            return await _journals.ExecuteInTransactionAsync(async token =>
            {
                var entry = await _journals.GetByIdAsync(command.JournalEntryId, token)
                    ?? throw new JournalValidationException(
                        JournalErrorCodes.JournalEntryNotFound,
                        $"Journal entry '{command.JournalEntryId}' was not found in this tenant.");

                if (entry.CompanyId != command.CompanyId)
                {
                    throw new JournalValidationException(
                        JournalErrorCodes.JournalEntryNotFound,
                        $"Journal entry '{entry.VoucherNo}' does not belong to company "
                        + $"'{command.CompanyId}'.");
                }

                JournalPosting.EnsureRowVersion(entry, command.RowVersion);

                // spec AC-04 / plan.md §3: the period lock is the FIRST data gate, before any
                // GLEntry line exists, so a back-dated submission modifies zero data.
                var company = await _companies.GetByIdAsync(entry.CompanyId, token)
                    ?? throw new JournalValidationException(
                        JournalErrorCodes.CompanyNotFound,
                        $"Company '{entry.CompanyId}' was not found in this tenant.");

                company.EnsurePostingDateUnlocked(entry.PostingDate);
                // R-13 FC-04: closed fiscal year rejects the posting too (second half of plan.md §3).
                await _companies.EnsurePostingDateInOpenYearAsync(company.Id, entry.PostingDate, token);

                // spec AC-02 / plan.md §3 canonical logic: the DRAFT's own lines must balance.
                entry.EnsureBalanced();

                // spec AC-03 / plan.md §3: leaf, active, same-company accounts only.
                var accountsById = await JournalPosting.LoadAccountsAsync(_accounts, entry, token);
                JournalEntryValidator.EnsurePostableAccounts(accountsById.Values, entry.CompanyId);

                // spec AC-01: only now does the state machine move Draft -> Submitted.
                entry.Submit();

                var glLines = JournalPosting.BuildLedgerLines(entry, accountsById, isReversal: false);

                // Constitution III.1 on the rows that are about to be written.
                DoubleEntryGuard.EnsureBalanced(glLines);

                await _journals.UpdateAsync(entry, token);
                await _journals.AddGlEntriesAsync(glLines, token);

                return Result<JournalEntryDto>.Success(JournalEntryDto.Build(entry));
            }, cancellationToken);
        }
        catch (JournalValidationException ex)
        {
            return Result<JournalEntryDto>.Failure(ex.Code, ex.Message);
        }
        catch (InvalidPostingAccountException ex)
        {
            // Spec AC-03: posting_to_group_account_prohibited (mapped to 400 by the controller).
            return Result<JournalEntryDto>.Failure(ex.Code, ex.Message);
        }
        catch (DoubleEntryImbalanceException ex)
        {
            // Spec AC-02: thrown before any row existed, so the failure carries zero writes.
            return Result<JournalEntryDto>.Failure(ex.Code, ex.Message);
        }
        catch (FiscalPeriodLockedException ex)
        {
            // tasks.md 2.2 / spec AC-04: the service threw BEFORE building any GLEntry line.
            return Result<JournalEntryDto>.Failure(ex.Code, ex.Message);
        }
        catch (ConcurrencyConflictException ex)
        {
            // The voucher changed between our load and our save (or the client sent a stale
            // token) - the API maps this to 409 concurrency_conflict.
            return Result<JournalEntryDto>.Failure(ex.Code, ex.Message);
        }
    }
}
