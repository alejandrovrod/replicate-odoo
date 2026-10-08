using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.FiscalClosing;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.GeneralLedger.PeriodClosing;

/// <summary>
/// Executes <see cref="CancelPeriodClosingVoucherCommand"/> (R-13 rewrite, spec FC-06): the old
/// handler set <c>IsCancelled = true</c> on the originals, violating the append-only ledger
/// (Constitution III.2). Cancellation now APPENDS mirrored reversal rows (Debit/Credit swapped,
/// same <c>VoucherNo</c>, original <c>PostingDate</c>, remarks prefixed <c>Reversal of …</c>) and
/// flips the voucher to <c>Cancelled</c>; the originals stay byte-identical. The fiscal year stays
/// OPEN — re-close requires a new voucher. Refused when the year is closed or the date frozen.
/// </summary>
public class CancelPeriodClosingVoucherCommandHandler : ICommandHandler<CancelPeriodClosingVoucherCommand, Result<PeriodClosingVoucherDto>>
{
    private const string ClosingVoucherType = "PeriodClosingVoucher";

    private readonly IPeriodClosingVoucherRepository _repository;
    private readonly IFiscalYearRepository _years;
    private readonly ICompanyRepository _companies;

    public CancelPeriodClosingVoucherCommandHandler(
        IPeriodClosingVoucherRepository repository,
        IFiscalYearRepository years,
        ICompanyRepository companies)
    {
        _repository = repository;
        _years = years;
        _companies = companies;
    }

    public async Task<Result<PeriodClosingVoucherDto>> HandleAsync(CancelPeriodClosingVoucherCommand request, CancellationToken cancellationToken = default)
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

                FiscalClosingGuards.EnsureRowVersion(
                    voucher.RowVersion, request.RowVersion, nameof(PeriodClosingVoucher), voucher.Id);

                voucher.EnsureCanCancel();

                var year = await _years.GetByIdAsync(voucher.FiscalYearId, cancellationToken)
                    ?? throw new FiscalClosingValidationException(
                        FiscalClosingErrorCodes.FiscalYearNotFound,
                        $"Fiscal year '{voucher.FiscalYearId}' was not found in this tenant.");

                // A closed year is immutable: cancel is refused, the close stands (spec FC-06).
                if (year.IsClosed)
                {
                    throw new FiscalYearClosedException(year.Id, year.YearName);
                }

                var company = await _companies.GetByIdAsync(voucher.CompanyId, cancellationToken)
                    ?? throw new FiscalClosingValidationException(
                        FiscalClosingErrorCodes.PeriodClosingNotFound,
                        $"Company '{voucher.CompanyId}' was not found in this tenant.");

                company.EnsurePostingDateUnlocked(voucher.PostingDate);
                await _years.EnsurePostingDateInOpenYearAsync(voucher.CompanyId, voucher.PostingDate, cancellationToken);

                var originals = await _repository.GetGLEntriesByVoucherAsync(voucher.Id, cancellationToken);

                var reversals = originals
                    .Select(entry => new GLEntry
                    {
                        TenantId = entry.TenantId,
                        CompanyId = entry.CompanyId,
                        AccountId = entry.AccountId,
                        PostingDate = entry.PostingDate,
                        Debit = entry.Credit,
                        Credit = entry.Debit,
                        DebitInAccountCurrency = entry.CreditInAccountCurrency,
                        CreditInAccountCurrency = entry.DebitInAccountCurrency,
                        AccountCurrency = entry.AccountCurrency,
                        VoucherType = ClosingVoucherType,
                        VoucherNo = entry.VoucherNo,
                        VoucherId = voucher.Id,
                        IsCancelled = false,
                        Remarks = $"Reversal of {ClosingVoucherType} {entry.VoucherNo} (cancelled)",
                        CreatedAt = DateTimeOffset.UtcNow,
                    })
                    .ToList();

                await _repository.AddGLEntriesAsync(reversals, cancellationToken);

                voucher.DocumentStatus = DocumentStatus.Cancelled;
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
        catch (ConcurrencyConflictException ex)
        {
            return Result<PeriodClosingVoucherDto>.Failure(ex.Code, ex.Message);
        }
    }
}
