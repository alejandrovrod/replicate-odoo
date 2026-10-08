using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.FiscalClosing;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using Erp.Domain.Services;

namespace Erp.Application.Features.GeneralLedger.PeriodClosing;

/// <summary>
/// Executes <see cref="SubmitPeriodClosingVoucherCommand"/> (R-13 rewrite, spec §7 audit): the
/// naïve handler grouped unbounded P&amp;L, never checked the fiscal year, the freeze date, the
/// retained leaf, the balance law, idempotency or RowVersion. The validation order below is
/// deliberate — every gate runs BEFORE the first row is built, so each rejection persists NOTHING:
/// <list type="number">
/// <item>existence + company ownership (<c>period_closing_not_found</c>);</item>
/// <item>idempotent replay: Submitted + same key → recorded success, zero new rows (spec FC-05/FC-09);</item>
/// <item>client RowVersion compare-and-swap (<c>concurrency_conflict</c>);</item>
/// <item>state machine <c>EnsureCanSubmit</c> (Draft-only);</item>
/// <item>fiscal-year existence + same company, open year, date containment (FC-04);</item>
/// <item>plan.md §3 hard lock: freeze date first, then the closed-year guard;</item>
/// <item>duplicate-year guard (<c>duplicate_closing_for_fiscal_year</c>, FC-05);</item>
/// <item>FY-windowed P&amp;L read, non-empty (<c>no_closing_balances</c>, FC-12);</item>
/// <item>retained leaf re-validation (FC-03);</item>
/// <item><c>PeriodClosingCalculator</c> + ΣD==ΣC assertion (FC-01/FC-02);</item>
/// <item>serializable persist: lines snapshot + GL + <c>Submitted</c> + <c>VoucherNo</c>.</item>
/// </list>
/// </summary>
public class SubmitPeriodClosingVoucherCommandHandler : ICommandHandler<SubmitPeriodClosingVoucherCommand, Result<PeriodClosingVoucherDto>>
{
    private const string ClosingVoucherType = "PeriodClosingVoucher";

    private readonly IPeriodClosingVoucherRepository _repository;
    private readonly IFiscalYearRepository _years;
    private readonly ICompanyRepository _companies;
    private readonly IAccountRepository _accounts;

    public SubmitPeriodClosingVoucherCommandHandler(
        IPeriodClosingVoucherRepository repository,
        IFiscalYearRepository years,
        ICompanyRepository companies,
        IAccountRepository accounts)
    {
        _repository = repository;
        _years = years;
        _companies = companies;
        _accounts = accounts;
    }

    public async Task<Result<PeriodClosingVoucherDto>> HandleAsync(SubmitPeriodClosingVoucherCommand request, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _repository.ExecuteInSerializableTransactionAsync(async () =>
            {
                var voucher = await _repository.GetByIdAsync(request.VoucherId, cancellationToken)
                    ?? throw new FiscalClosingValidationException(
                        FiscalClosingErrorCodes.PeriodClosingNotFound,
                        $"Closing voucher '{request.VoucherId}' was not found in this tenant.");

                if (voucher.CompanyId != request.CompanyId)
                {
                    throw new FiscalClosingValidationException(
                        FiscalClosingErrorCodes.PeriodClosingNotFound,
                        $"Closing voucher '{voucher.VoucherNo}' does not belong to company '{request.CompanyId}'.");
                }

                // Spec FC-05/FC-09: a replayed submit after commit returns the recorded success
                // WITHOUT new GL rows (the HTTP idempotency filter covers cross-request replays;
                // this covers same-key resubmits of an already-submitted voucher).
                if (voucher.DocumentStatus == DocumentStatus.Submitted
                    && request.IdempotencyKey is not null
                    && string.Equals(voucher.IdempotencyKey, request.IdempotencyKey, StringComparison.Ordinal))
                {
                    return Result<PeriodClosingVoucherDto>.Success(PeriodClosingVoucherDto.Build(voucher));
                }

                FiscalClosingGuards.EnsureRowVersion(
                    voucher.RowVersion, request.RowVersion, nameof(PeriodClosingVoucher), voucher.Id);

                voucher.EnsureCanSubmit();

                var year = await _years.GetByIdAsync(voucher.FiscalYearId, cancellationToken)
                    ?? throw new FiscalClosingValidationException(
                        FiscalClosingErrorCodes.FiscalYearNotFound,
                        $"Fiscal year '{voucher.FiscalYearId}' was not found in this tenant.");

                if (year.CompanyId != voucher.CompanyId)
                {
                    throw new FiscalClosingValidationException(
                        FiscalClosingErrorCodes.FiscalYearNotFound,
                        $"Fiscal year '{year.YearName}' does not belong to company '{voucher.CompanyId}'.");
                }

                if (year.IsClosed)
                {
                    throw new FiscalYearClosedException(year.Id, year.YearName);
                }

                if (voucher.PostingDate < year.StartDate || voucher.PostingDate > year.EndDate)
                {
                    throw new ClosingDateOutsideFiscalYearException(
                        voucher.PostingDate, year.YearName, year.StartDate, year.EndDate);
                }

                var company = await _companies.GetByIdAsync(voucher.CompanyId, cancellationToken)
                    ?? throw new FiscalClosingValidationException(
                        FiscalClosingErrorCodes.PeriodClosingNotFound,
                        $"Company '{voucher.CompanyId}' was not found in this tenant.");

                company.EnsurePostingDateUnlocked(voucher.PostingDate);
                await _years.EnsurePostingDateInOpenYearAsync(voucher.CompanyId, voucher.PostingDate, cancellationToken);

                if (await _repository.HasSubmittedCloseAsync(voucher.CompanyId, year.Id, voucher.Id, cancellationToken))
                {
                    throw new DuplicateClosingForFiscalYearException(year.Id);
                }

                var balances = await _repository.GetUnclosedPLBalancesAsync(voucher.CompanyId, year.Id, cancellationToken);
                if (balances.Count == 0)
                {
                    throw new NoClosingBalancesException(year.YearName);
                }

                var retained = await FiscalClosingGuards.ResolveRetainedEarningsAsync(
                    _accounts, company, voucher.RetainedEarningsAccountId, cancellationToken);

                var computation = PeriodClosingCalculator.Compute(
                    balances.Select(b => new ClosingBalanceInput(b.AccountId, b.RootType, b.Balance)).ToList(),
                    retained.Id);

                voucher.VoucherNo = await _repository.NextClosingVoucherNumberAsync(voucher.CompanyId, year, cancellationToken);
                if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
                {
                    voucher.IdempotencyKey = request.IdempotencyKey;
                }

                var glEntries = new List<GLEntry>(computation.OffsetLines.Count + 1);
                var lines = new List<PeriodClosingVoucherLine>(computation.OffsetLines.Count + 1);

                foreach (var offset in computation.OffsetLines)
                {
                    glEntries.Add(BuildGLEntry(voucher, offset.AccountId, offset.Debit, offset.Credit));
                    lines.Add(BuildLine(voucher, offset.AccountId, offset.Debit, offset.Credit));
                }

                if (computation.RetainedLine is not null)
                {
                    glEntries.Add(BuildGLEntry(
                        voucher, retained.Id, computation.RetainedLine.Debit, computation.RetainedLine.Credit));
                    lines.Add(BuildLine(
                        voucher, retained.Id, computation.RetainedLine.Debit, computation.RetainedLine.Credit));
                }

                await _repository.AddLinesAsync(lines, cancellationToken);
                await _repository.AddGLEntriesAsync(glEntries, cancellationToken);

                voucher.RetainedEarningsAccountId = retained.Id;
                voucher.DocumentStatus = DocumentStatus.Submitted;
                _repository.Update(voucher);

                return Result<PeriodClosingVoucherDto>.Success(PeriodClosingVoucherDto.Build(voucher));
            }, cancellationToken);
        }
        catch (FiscalClosingException ex)
        {
            return Result<PeriodClosingVoucherDto>.Failure(ex.Code, ex.Message);
        }
        catch (FiscalPeriodLockedException ex)
        {
            return Result<PeriodClosingVoucherDto>.Failure(ex.Code, ex.Message);
        }
        catch (DoubleEntryImbalanceException ex)
        {
            return Result<PeriodClosingVoucherDto>.Failure(ex.Code, ex.Message);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Result<PeriodClosingVoucherDto>.Failure(ex.Code, ex.Message);
        }
    }

    private static GLEntry BuildGLEntry(PeriodClosingVoucher voucher, Guid accountId, decimal debit, decimal credit) =>
        new()
        {
            TenantId = voucher.TenantId,
            CompanyId = voucher.CompanyId,
            AccountId = accountId,
            PostingDate = voucher.PostingDate,
            Debit = debit,
            Credit = credit,
            DebitInAccountCurrency = debit,
            CreditInAccountCurrency = credit,
            AccountCurrency = "USD",
            VoucherType = ClosingVoucherType,
            VoucherNo = voucher.VoucherNo,
            VoucherId = voucher.Id,
            IsCancelled = false,
            Remarks = voucher.Remarks ?? "Period Closing Offset",
            CreatedAt = DateTimeOffset.UtcNow,
        };

    private static PeriodClosingVoucherLine BuildLine(PeriodClosingVoucher voucher, Guid accountId, decimal debit, decimal credit) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = voucher.TenantId,
            VoucherId = voucher.Id,
            AccountId = accountId,
            Debit = debit,
            Credit = credit,
        };
}
