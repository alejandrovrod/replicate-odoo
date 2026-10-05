using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using Erp.Domain.Services;

namespace Erp.Application.Features.HrPayroll.Commands;

/// <summary>
/// Executes <see cref="DisbursePayrollCommand"/> (Task 12.4, spec HR-02 Phase 2): Submitted
/// gate, the frozen-period gate, bank GL resolution (05-banking <see cref="BankAccount.GLAccountId"/>
/// read-only), the exact Dr 2150 / Cr bank pair and the Submitted -&gt; Paid transition - all
/// inside ONE <c>IHrPayrollRepository</c> transaction, so a rejected disbursal writes zero rows.
/// </summary>
public sealed class DisbursePayrollCommandHandler
    : ICommandHandler<DisbursePayrollCommand, Result<PayrollEntryDto>>
{
    private const string VoucherType = "Payroll";
    private const string VoucherPrefix = "PYR";

    private readonly ICompanyRepository _companies;
    private readonly IAccountRepository _accounts;
    private readonly IBankRepository _banks;
    private readonly IHrPayrollRepository _hr;

    public DisbursePayrollCommandHandler(
        ICompanyRepository companies,
        IAccountRepository accounts,
        IBankRepository banks,
        IHrPayrollRepository hr)
    {
        _companies = companies;
        _accounts = accounts;
        _banks = banks;
        _hr = hr;
    }

    public async Task<Result<PayrollEntryDto>> HandleAsync(
        DisbursePayrollCommand command,
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
                        $"Only a Submitted payroll entry can be disbursed; entry '{entry.PayrollNumber}' is '{entry.Status}'.");
                }

                var company = await _companies.GetByIdAsync(command.CompanyId, token)
                    ?? throw new HrValidationException(
                        HrPayrollErrorCodes.CompanyNotFound,
                        $"Company '{command.CompanyId}' was not found in this tenant.");

                // tasks.md 2.2 / spec AC-04: hard fiscal period lock - before a single GLEntry
                // line is built, so a back-dated disbursal modifies ZERO data.
                company.EnsurePostingDateUnlocked(command.PostingDate);

                var bankAccount = await _banks.GetAccountByIdAsync(command.BankAccountId, token)
                    ?? throw new HrValidationException(
                        HrPayrollErrorCodes.BankAccountNotFound,
                        $"Bank account '{command.BankAccountId}' was not found in this tenant.");

                if (bankAccount.CompanyId != company.Id)
                {
                    throw new HrValidationException(
                        HrPayrollErrorCodes.BankAccountNotFound,
                        $"Bank account '{bankAccount.AccountName}' does not belong to company '{company.Id}'.");
                }

                var bankGlAccount = await HrAccountGuards.RequirePostableAccountAsync(
                    _accounts, bankAccount.GLAccountId, company.Id,
                    $"bank account '{bankAccount.AccountName}'", token);

                var payableAccount = await HrAccountGuards.RequireAccountByCodeAsync(
                    _accounts, company.Id, company.PayrollPayableAccountCode,
                    "Company.PayrollPayableAccountCode", token);

                // Spec HR-02 Phase 2, exact: Dr 2150 TotalNetPay / Cr bank TotalNetPay.
                var voucherNo = await _hr.NextVoucherNumberAsync(
                    company.Id, VoucherPrefix, command.PostingDate.Year, token);

                var amount = Round4(entry.TotalNetPay);
                var glLines = new List<GLEntry>(2)
                {
                    NewGlLine(company.Id, entry, payableAccount, voucherNo, command.PostingDate,
                        $"Payroll disbursement {entry.PayrollNumber}: payable cleared", debit: amount, credit: 0m),
                    NewGlLine(company.Id, entry, bankGlAccount, voucherNo, command.PostingDate,
                        $"Payroll disbursement {entry.PayrollNumber}: bank payout", debit: 0m, credit: amount),
                };

                // Constitution III.1: the pair balances BEFORE anything is saved.
                DoubleEntryGuard.EnsureBalanced(glLines);

                await _hr.AddGlEntriesAsync(glLines, token);

                entry.MarkPaid();
                entry.PaymentVoucherNo = voucherNo;
                await _hr.UpdatePayrollEntryAsync(entry, token);

                var slips = await _hr.GetSlipsByEntryAsync(entry.Id, token);
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

    private static GLEntry NewGlLine(
        Guid companyId,
        PayrollEntry entry,
        Account account,
        string voucherNo,
        DateOnly postingDate,
        string remarks,
        decimal debit,
        decimal credit) =>
        new()
        {
            CompanyId = companyId,
            PostingDate = postingDate,
            AccountId = account.Id,
            Account = account,
            Debit = debit,
            Credit = credit,
            DebitInAccountCurrency = debit,
            CreditInAccountCurrency = credit,
            AccountCurrency = account.Currency,
            VoucherType = VoucherType,
            VoucherNo = voucherNo,
            VoucherId = entry.Id,
            PartyType = null,
            PartyId = null,
            CostCenterId = null,
            IsCancelled = false,
            Remarks = remarks,
        };

    private static decimal Round4(decimal value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);
}
