using Erp.Application.Common;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Stock.Commands;

public sealed class CancelStockEntryCommandHandler : ICommandHandler<CancelStockEntryCommand, Result<bool>>
{
    private readonly IStockRepository _stock;
    private readonly ICompanyRepository _companies;
    private readonly ISerialNoRepository? _serials;

    public CancelStockEntryCommandHandler(
        IStockRepository stock,
        ICompanyRepository companies,
        ISerialNoRepository? serials = null)
    {
        _stock = stock;
        _companies = companies;
        // Optional for legacy direct constructions (older tests post untracked items only):
        // serial revert is skipped when unwired, batch quantities still revert through the
        // compensating Kardex rows below.
        _serials = serials;
    }

    public async Task<Result<bool>> HandleAsync(CancelStockEntryCommand request, CancellationToken cancellationToken)
    {
        return await _stock.ExecuteInTransactionAsync(async token =>
        {
            var entry = await _stock.GetEntryByIdAsync(request.StockEntryId, token);
            if (entry is null)
            {
                return Result<bool>.Failure(
                    StockErrorCodes.VoucherNotFound, 
                    $"Stock entry '{request.StockEntryId}' not found.");
            }

            if (entry.CompanyId != request.CompanyId)
            {
                return Result<bool>.Failure(
                    StockErrorCodes.VoucherNotFound, 
                    $"Stock entry '{request.StockEntryId}' does not belong to company '{request.CompanyId}'.");
            }

            if (entry.IsCancelled)
            {
                return Result<bool>.Failure(
                    StockErrorCodes.InvalidStatusTransition, 
                    $"Stock entry '{entry.VoucherNo}' is already cancelled.");
            }

            var company = await _companies.GetByIdAsync(entry.CompanyId, token);
            if (company is null)
            {
                return Result<bool>.Failure(
                    StockErrorCodes.CompanyNotFound, 
                    $"Company '{entry.CompanyId}' not found.");
            }

            try
            {
                company.EnsurePostingDateUnlocked(entry.PostingDate);
                // R-13 FC-04: cancelling into a closed year is refused — the close is immutable.
                await _companies.EnsurePostingDateInOpenYearAsync(company.Id, entry.PostingDate, token);
            }
            catch (FiscalPeriodLockedException ex)
            {
                return Result<bool>.Failure(ex.Code, ex.Message);
            }
            catch (FiscalClosingException ex)
            {
                return Result<bool>.Failure(ex.Code, ex.Message);
            }

            var oldSles = await _stock.GetLedgerEntriesByVoucherAsync(entry.VoucherNo, token);
            var oldGls = await _stock.GetGlEntriesByVoucherIdAsync(entry.Id, token);

            var newSles = new List<StockLedgerEntry>(oldSles.Count);
            foreach (var sle in oldSles)
            {
                newSles.Add(new StockLedgerEntry
                {
                    Id = Guid.NewGuid(),
                    TenantId = sle.TenantId,
                    ItemId = sle.ItemId,
                    WarehouseId = sle.WarehouseId,
                    BatchId = sle.BatchId,
                    StockEntryId = sle.StockEntryId,
                    VoucherType = sle.VoucherType,
                    VoucherNo = sle.VoucherNo,
                    PostingDate = sle.PostingDate,
                    QtyChange = -sle.QtyChange,
                    ValuationRate = sle.ValuationRate,
                    Amount = -sle.Amount,
                    CreatedAt = DateTimeOffset.UtcNow,
                    IsCancelled = true
                });
            }

            var newGls = new List<GLEntry>(oldGls.Count);
            foreach (var gl in oldGls)
            {
                newGls.Add(new GLEntry
                {
                    TenantId = gl.TenantId,
                    CompanyId = gl.CompanyId,
                    PostingDate = gl.PostingDate,
                    AccountId = gl.AccountId,
                    Debit = gl.Credit,
                    Credit = gl.Debit,
                    DebitInAccountCurrency = gl.CreditInAccountCurrency,
                    CreditInAccountCurrency = gl.DebitInAccountCurrency,
                    AccountCurrency = gl.AccountCurrency,
                    VoucherType = gl.VoucherType,
                    VoucherNo = gl.VoucherNo,
                    VoucherId = gl.VoucherId,
                    PartyType = gl.PartyType,
                    PartyId = gl.PartyId,
                    CostCenterId = gl.CostCenterId,
                    IsCancelled = true,
                    Remarks = $"Cancelled: {gl.Remarks}",
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }

            entry.IsCancelled = true;

            await _stock.UpdateStockEntryAsync(entry, token);
            await _stock.AddLedgerEntriesAsync(newSles, token);
            await _stock.AddGlEntriesAsync(newGls, token);

            // Batch-and-serial Fase 3: batch quantities revert through the compensating rows
            // above (derived balances); serials revert by movement direction - receipt rows
            // (created-new by the global-uniqueness boundary) are deleted, delivered rows go
            // back to Active with their dispatch refs cleared, transferred rows move back to
            // the source warehouse.
            if (_serials is not null)
            {
                await RevertSerialsAsync(entry, token);
            }

            return Result<bool>.Success(true);
        }, cancellationToken);
    }

    private async Task RevertSerialsAsync(StockEntry entry, CancellationToken token)
    {
        var lineIds = entry.Items.Select(l => l.Id).ToList();
        if (lineIds.Count == 0)
        {
            return;
        }

        var serials = await _serials!.GetByStockEntryItemIdsAsync(lineIds, token);
        if (serials.Count == 0)
        {
            return;
        }

        switch (entry.EntryType)
        {
            case StockEntryType.MaterialReceipt:
                // Only never-moved rows are still linked here (any onward move re-links to
                // the moving line), so deletion is exact.
                await _serials.DeleteRangeAsync(serials, token);
                break;
            case StockEntryType.MaterialIssue:
                foreach (var serial in serials)
                {
                    serial.Status = SerialNoStatus.Active;
                    serial.DeliveryDate = null;
                    serial.DeliveryDocumentNo = null;
                    serial.StockEntryItemId = null;
                }

                await _serials.UpdateRangeAsync(serials, token);
                break;
            case StockEntryType.MaterialTransfer:
                foreach (var serial in serials)
                {
                    serial.CurrentWarehouseId = entry.WarehouseId;
                    serial.StockEntryItemId = null;
                }

                await _serials.UpdateRangeAsync(serials, token);
                break;
            default:
                break;
        }
    }
}
