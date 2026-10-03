using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using Erp.Domain.Services;

namespace Erp.Application.Features.GeneralLedger.Commands;

/// <summary>
/// Executes <see cref="CancelJournalEntryCommand"/>: Submitted -&gt; Cancelled plus the
/// compensating reversal append, inside ONE transaction (spec AC-07 / Constitution III.3).
/// </summary>
/// <remarks>
/// <para><b>Reversal posting date.</b> The reversal rows carry the voucher's ORIGINAL
/// <c>PostingDate</c>, so <c>Company.EnsurePostingDateUnlocked</c> is checked against that date:
/// a frozen original period blocks the CANCELLATION too - the literal spec AC-04 wording
/// ("posting, modification, or cancellation is blocked"). The status gate runs after the freeze
/// gate for the same reason the submit path orders it that way: the lock is unconditional.</para>
/// <para><b>No account postability re-check.</b> The accounts were vetted at submit and the
/// <c>FK_GLEntry_Account</c>/<c>FK_JournalEntryLine_Account</c> constraints (Restrict) guarantee
/// they still exist; deactivating or re-grouping an account must never make a voucher
/// un-cancellable. Only the currency snapshot of the reversal rows needs the account row.</para>
/// </remarks>
public sealed class CancelJournalEntryCommandHandler
    : ICommandHandler<CancelJournalEntryCommand, Result<JournalEntryDto>>
{
    private readonly ICompanyRepository _companies;
    private readonly IAccountRepository _accounts;
    private readonly IJournalRepository _journals;

    public CancelJournalEntryCommandHandler(
        ICompanyRepository companies,
        IAccountRepository accounts,
        IJournalRepository journals)
    {
        _companies = companies;
        _accounts = accounts;
        _journals = journals;
    }

    public async Task<Result<JournalEntryDto>> HandleAsync(
        CancelJournalEntryCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // One transaction for status + reversal rows: the voucher ends up Cancelled with a
            // complete mirror image, or untouched - never half-cancelled.
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

                // spec AC-04: cancelling WRITES ledger rows (the reversal), so the period lock
                // applies - against the voucher's ORIGINAL PostingDate, which is the date the
                // reversal rows will carry.
                var company = await _companies.GetByIdAsync(entry.CompanyId, token)
                    ?? throw new JournalValidationException(
                        JournalErrorCodes.CompanyNotFound,
                        $"Company '{entry.CompanyId}' was not found in this tenant.");

                company.EnsurePostingDateUnlocked(entry.PostingDate);

                // spec AC-07: only a Submitted voucher cancels; Draft/Cancelled -> 409.
                entry.Cancel();

                var accountsById = await JournalPosting.LoadAccountsAsync(_accounts, entry, token);
                var reversalLines = JournalPosting.BuildLedgerLines(
                    entry, accountsById, isReversal: true);

                // Constitution III.1 on the rows that are about to be written (a mirror of a
                // balanced voucher is balanced, but the guard proves it rather than assuming it).
                DoubleEntryGuard.EnsureBalanced(reversalLines);

                await _journals.UpdateAsync(entry, token);
                await _journals.AddGlEntriesAsync(reversalLines, token);

                return Result<JournalEntryDto>.Success(JournalEntryDto.Build(entry));
            }, cancellationToken);
        }
        catch (JournalValidationException ex)
        {
            return Result<JournalEntryDto>.Failure(ex.Code, ex.Message);
        }
        catch (DoubleEntryImbalanceException ex)
        {
            return Result<JournalEntryDto>.Failure(ex.Code, ex.Message);
        }
        catch (FiscalPeriodLockedException ex)
        {
            return Result<JournalEntryDto>.Failure(ex.Code, ex.Message);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Result<JournalEntryDto>.Failure(ex.Code, ex.Message);
        }
    }
}
