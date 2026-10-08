using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Erp.Application.Common;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.GeneralLedger.PeriodClosing;

public class SubmitPeriodClosingVoucherCommandHandler : ICommandHandler<SubmitPeriodClosingVoucherCommand, Result<bool>>
{
    private readonly IPeriodClosingVoucherRepository _repository;

    public SubmitPeriodClosingVoucherCommandHandler(IPeriodClosingVoucherRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<bool>> HandleAsync(SubmitPeriodClosingVoucherCommand request, CancellationToken cancellationToken = default)
    {
        return await _repository.ExecuteInTransactionAsync(async () =>
        {
            var voucher = await _repository.GetByIdAsync(request.VoucherId, cancellationToken);
            if (voucher == null) return Result<bool>.Failure(new Error("Voucher.NotFound", "Voucher not found."));
            if (voucher.DocumentStatus != DocumentStatus.Draft) return Result<bool>.Failure(new Error("Voucher.InvalidStatus", "Voucher is not in Draft status."));

            var entries = await _repository.GetUnclosedPLEntriesAsync(voucher.CompanyId, voucher.PostingDate, cancellationToken);
            var plBalances = entries
                .GroupBy(x => x.AccountId)
                .Select(g => new { AccountId = g.Key, Balance = g.Sum(e => e.Debit - e.Credit) })
                .Where(x => x.Balance != 0)
                .ToList();

            if (!plBalances.Any()) return Result<bool>.Failure(new Error("Voucher.NoBalances", "No P&L balances found to close."));

            decimal totalNetBalance = 0;
            var offsetEntries = new List<GLEntry>();

            foreach (var bal in plBalances)
            {
                totalNetBalance += bal.Balance;
                offsetEntries.Add(new GLEntry
                {
                    TenantId = voucher.TenantId,
                    CompanyId = voucher.CompanyId,
                    AccountId = bal.AccountId,
                    PostingDate = voucher.PostingDate,
                    VoucherType = "PeriodClosingVoucher",
                    VoucherNo = voucher.VoucherNo,
                    Remarks = voucher.Remarks ?? "Period Closing Offset",
                    Debit = bal.Balance < 0 ? -bal.Balance : 0,
                    Credit = bal.Balance > 0 ? bal.Balance : 0,
                    IsCancelled = false,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }

            offsetEntries.Add(new GLEntry
            {
                TenantId = voucher.TenantId,
                CompanyId = voucher.CompanyId,
                AccountId = voucher.RetainedEarningsAccountId,
                PostingDate = voucher.PostingDate,
                VoucherType = "PeriodClosingVoucher",
                VoucherNo = voucher.VoucherNo,
                Remarks = voucher.Remarks ?? "Period Closing Net Retained Earnings",
                Debit = totalNetBalance > 0 ? totalNetBalance : 0,
                Credit = totalNetBalance < 0 ? -totalNetBalance : 0,
                IsCancelled = false,
                CreatedAt = DateTimeOffset.UtcNow
            });

            await _repository.AddGLEntriesAsync(offsetEntries, cancellationToken);
            voucher.DocumentStatus = DocumentStatus.Submitted;
            _repository.Update(voucher);

            return Result<bool>.Success(true);
        }, cancellationToken);
    }
}
