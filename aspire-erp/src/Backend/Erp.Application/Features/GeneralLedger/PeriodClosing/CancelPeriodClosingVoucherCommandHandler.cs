using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Erp.Application.Common;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.GeneralLedger.PeriodClosing;

public class CancelPeriodClosingVoucherCommandHandler : ICommandHandler<CancelPeriodClosingVoucherCommand, Result<bool>>
{
    private readonly IPeriodClosingVoucherRepository _repository;

    public CancelPeriodClosingVoucherCommandHandler(IPeriodClosingVoucherRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<bool>> HandleAsync(CancelPeriodClosingVoucherCommand request, CancellationToken cancellationToken = default)
    {
        return await _repository.ExecuteInTransactionAsync(async () =>
        {
            var voucher = await _repository.GetByIdAsync(request.VoucherId, cancellationToken);
            if (voucher == null) return Result<bool>.Failure(new Error("Voucher.NotFound", "Voucher not found."));
            if (voucher.DocumentStatus != DocumentStatus.Submitted) return Result<bool>.Failure(new Error("Voucher.InvalidStatus", "Only submitted vouchers can be cancelled."));

            var entries = await _repository.GetGLEntriesByVoucherAsync(voucher.VoucherNo, cancellationToken);
            foreach (var entry in entries)
            {
                entry.IsCancelled = true;
            }
            _repository.UpdateGLEntries(entries);

            voucher.DocumentStatus = DocumentStatus.Cancelled;
            _repository.Update(voucher);

            return Result<bool>.Success(true);
        }, cancellationToken);
    }
}
