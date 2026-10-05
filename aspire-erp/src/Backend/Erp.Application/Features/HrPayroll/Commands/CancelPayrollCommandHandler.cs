using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using Erp.Domain.Services;

namespace Erp.Application.Features.HrPayroll.Commands;

/// <summary>
/// Executes <see cref="CancelPayrollCommand"/> (Task 12.4, spec HR-05): Submitted-only gate,
/// the frozen-period gate, the accrual mirror (every line swapped, component lines AND the
/// payable line), slips -&gt; Cancelled and entry -&gt; Cancelled - all inside ONE
/// <c>IHrPayrollRepository</c> transaction, so a rejected cancel writes zero rows.
/// </summary>
/// <remarks>
/// The mirror follows the disposal-reversal precedent (swap every original line into a NEW
/// voucher, flag the reversal rows, leave every original row byte-identical - Constitution
/// III.2 forbids touching them). The reversal reuses the PYR sequence with a "Reversal of"
/// remark (the reversal voucher number stays gapless); the double-cancel and cancel-Paid
/// paths fail with <c>invalid_status_transition</c> (409) and write nothing.
/// </remarks>
public sealed class CancelPayrollCommandHandler
    : ICommandHandler<CancelPayrollCommand, Result<PayrollEntryDto>>
{
    private const string VoucherPrefix = "PYR";

    private readonly ICompanyRepository _companies;
    private readonly IHrPayrollRepository _hr;

    public CancelPayrollCommandHandler(
        ICompanyRepository companies,
        IHrPayrollRepository hr)
    {
        _companies = companies;
        _hr = hr;
    }

    public async Task<Result<PayrollEntryDto>> HandleAsync(
        CancelPayrollCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _hr.ExecuteInTransactionAsync(async token =>
            {
                var entry = await _hr.GetPayrollEntryByIdAsync(command.PayrollEntryId, token);

                // Company mismatch is reported as NOT FOUND: the id must not leak across companies.
                if (entry is null || entry.CompanyId != command.CompanyId)
                {
                    throw new HrValidationException(
                        HrPayrollErrorCodes.PayrollEntryNotFound,
                        $"Payroll entry '{command.PayrollEntryId}' was not found in company '{command.CompanyId}'.");
                }

                EnsureRowVersion(entry, command.RowVersion);

                if (entry.Status != PayrollEntryStatus.Submitted)
                {
                    throw new HrValidationException(
                        HrPayrollErrorCodes.InvalidStatusTransition,
                        $"Only a Submitted payroll entry can be cancelled; entry '{entry.PayrollNumber}' is '{entry.Status}'.");
                }

                var postingDate = command.PostingDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

                var company = await _companies.GetByIdAsync(command.CompanyId, token)
                    ?? throw new HrValidationException(
                        HrPayrollErrorCodes.CompanyNotFound,
                        $"Company '{command.CompanyId}' was not found in this tenant.");

                // tasks.md 2.2 / spec AC-04: hard fiscal period lock - before a single mirror
                // line is built or a single status flips, so a back-dated cancel writes ZERO rows.
                company.EnsurePostingDateUnlocked(postingDate);

                var accrualLines = await _hr.GetAccrualGlEntriesAsync(entry.Id, token);
                if (accrualLines.Count == 0)
                {
                    throw new HrValidationException(
                        HrPayrollErrorCodes.AccrualVoucherNotFound,
                        $"Payroll entry '{entry.PayrollNumber}' has no live accrual voucher to reverse.");
                }

                var accrualVoucherNo = accrualLines[0].VoucherNo;
                var reversalVoucherNo = await _hr.NextVoucherNumberAsync(
                    company.Id, VoucherPrefix, postingDate.Year, token);

                var mirror = accrualLines
                    .Select(original => new GLEntry
                    {
                        CompanyId = company.Id,
                        PostingDate = postingDate,
                        AccountId = original.AccountId,
                        Account = original.Account,
                        Debit = original.Credit,
                        Credit = original.Debit,
                        DebitInAccountCurrency = original.CreditInAccountCurrency,
                        CreditInAccountCurrency = original.DebitInAccountCurrency,
                        AccountCurrency = original.AccountCurrency,
                        VoucherType = original.VoucherType,
                        VoucherNo = reversalVoucherNo,
                        VoucherId = entry.Id,
                        PartyType = original.PartyType,
                        PartyId = original.PartyId,
                        CostCenterId = original.CostCenterId,
                        IsCancelled = true,
                        Remarks = $"Reversal of {accrualVoucherNo}: payroll run {entry.PayrollNumber} cancelled",
                    })
                    .ToList();

                // Constitution III.1: the mirror balances BEFORE anything is saved.
                DoubleEntryGuard.EnsureBalanced(mirror);

                await _hr.AddGlEntriesAsync(mirror, token);

                var slips = await _hr.GetSlipsByEntryAsync(entry.Id, token);
                foreach (var slip in slips)
                {
                    slip.Cancel();
                    await _hr.UpdateSlipAsync(slip, token);
                }

                entry.Cancel();
                await _hr.UpdatePayrollEntryAsync(entry, token);

                return Result<PayrollEntryDto>.Success(PayrollEntryDto.Build(entry, slips.Count));
            }, cancellationToken);
        }
        catch (HrValidationException ex)
        {
            return Result<PayrollEntryDto>.Failure(ex.Code, ex.Message);
        }
        catch (FiscalPeriodLockedException ex)
        {
            return Result<PayrollEntryDto>.Failure(ex.Code, ex.Message);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Result<PayrollEntryDto>.Failure(ex.Code, ex.Message);
        }
    }

    private static void EnsureRowVersion(PayrollEntry entry, byte[]? expected)
    {
        if (expected is null)
        {
            return;
        }

        if (entry.RowVersion is null || !entry.RowVersion.AsSpan().SequenceEqual(expected))
        {
            throw new ConcurrencyConflictException(nameof(PayrollEntry), entry.Id);
        }
    }
}
