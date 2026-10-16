using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using Erp.Domain.Services;

namespace Erp.Application.Features.LandedCost.Commands;

/// <summary>
/// Cancels a Submitted landed cost voucher (Fase 4): compensating GL mirror plus negated
/// Kardex revaluation rows (same voucher number, reversal flags). The FIFO replay absorbs
/// the negation symmetrically, restoring pre-submit layer rates.
/// </summary>
public sealed class CancelLandedCostVoucherCommandHandler
    : ICommandHandler<CancelLandedCostVoucherCommand, Result<LandedCostVoucherDto>>
{
    private readonly ILandedCostVoucherRepository _vouchers;
    private readonly IPurchaseRepository _purchases;
    private readonly ICompanyRepository _companies;
    private readonly IAccountRepository _accounts;
    private readonly IItemRepository _items;
    private readonly IStockRepository _stock;

    public CancelLandedCostVoucherCommandHandler(
        ILandedCostVoucherRepository vouchers,
        IPurchaseRepository purchases,
        ICompanyRepository companies,
        IAccountRepository accounts,
        IItemRepository items,
        IStockRepository stock)
    {
        _vouchers = vouchers;
        _purchases = purchases;
        _companies = companies;
        _accounts = accounts;
        _items = items;
        _stock = stock;
    }

    public async Task<Result<LandedCostVoucherDto>> HandleAsync(
        CancelLandedCostVoucherCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _vouchers.ExecuteInTransactionAsync(async token =>
            {
                var voucher = await _vouchers.GetByIdAsync(command.VoucherId, token)
                    ?? throw new LandedCostValidationException(
                        LandedCostErrorCodes.LandedCostVoucherNotFound,
                        $"Landed cost voucher '{command.VoucherId}' was not found in this tenant.");

                if (voucher.CompanyId != command.CompanyId)
                {
                    throw new LandedCostValidationException(
                        LandedCostErrorCodes.LandedCostVoucherNotFound,
                        $"Landed cost voucher '{command.VoucherId}' does not belong to company '{command.CompanyId}'.");
                }

                EnsureRowVersion(voucher, command.RowVersion);
                LandedCostValidator.EnsureCancellable(voucher.Status, voucher.VoucherNo);

                var company = await _companies.GetByIdAsync(voucher.CompanyId, token)
                    ?? throw new LandedCostValidationException(
                        LandedCostErrorCodes.LandedCostVoucherNotFound,
                        $"Company '{voucher.CompanyId}' was not found in this tenant.");

                company.EnsurePostingDateUnlocked(voucher.PostingDate);
                await _companies.EnsurePostingDateInOpenYearAsync(company.Id, voucher.PostingDate, token);

                var originalSles = await _stock.GetLedgerEntriesByVoucherAsync(voucher.VoucherNo, token);
                var originalGls = await _stock.GetGlEntriesByVoucherIdAsync(voucher.Id, token);

                var reversalSles = originalSles.Select(sle => new StockLedgerEntry
                {
                    Id = Guid.NewGuid(),
                    TenantId = sle.TenantId,
                    ItemId = sle.ItemId,
                    WarehouseId = sle.WarehouseId,
                    BatchId = sle.BatchId,
                    PostingDate = sle.PostingDate,
                    QtyChange = -sle.QtyChange,
                    ValuationRate = sle.ValuationRate,
                    Amount = -sle.Amount,
                    VoucherType = sle.VoucherType,
                    VoucherNo = sle.VoucherNo,
                    CreatedAt = DateTimeOffset.UtcNow,
                    IsCancelled = true,
                }).ToList();

                var reversalGls = originalGls.Select(gl => new GLEntry
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
                    IsCancelled = true,
                    Remarks = $"Cancelled: {gl.Remarks}",
                    CreatedAt = DateTimeOffset.UtcNow,
                }).ToList();

                DoubleEntryGuard.EnsureBalanced(reversalGls);

                voucher.Status = DocumentStatus.Cancelled;

                await _vouchers.UpdateAsync(voucher, token);
                await _stock.AddLedgerEntriesAsync(reversalSles, token);
                await _stock.AddGlEntriesAsync(reversalGls, token);

                var itemIds = voucher.Items.Select(i => i.ItemId).Distinct().ToList();
                var itemsById = (await _items.GetByIdsAsync(itemIds, token)).ToDictionary(i => i.Id);
                var receiptNumbers = new Dictionary<Guid, string>();
                foreach (var receiptId in voucher.Receipts.Select(r => r.PurchaseReceiptId).Distinct())
                {
                    var receipt = await _purchases.GetReceiptByIdAsync(receiptId, token);
                    if (receipt is not null)
                    {
                        receiptNumbers[receiptId] = receipt.VoucherNo;
                    }
                }

                foreach (var charge in voucher.Charges)
                {
                    charge.ExpenseAccount = await _accounts.GetByIdAsync(charge.ExpenseAccountId, token);
                }

                return Result<LandedCostVoucherDto>.Success(
                    LandedCostVoucherDto.Build(voucher, itemsById, receiptNumbers));
            }, cancellationToken);
        }
        catch (LandedCostValidationException ex)
        {
            return Result<LandedCostVoucherDto>.Failure(ex.Code, ex.Message);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Result<LandedCostVoucherDto>.Failure(ex.Code, ex.Message);
        }
        catch (FiscalPeriodLockedException ex)
        {
            return Result<LandedCostVoucherDto>.Failure(ex.Code, ex.Message);
        }
    }

    private static void EnsureRowVersion(LandedCostVoucher voucher, byte[]? expected)
    {
        if (expected is null)
        {
            return;
        }

        if (voucher.RowVersion is null || !voucher.RowVersion.AsSpan().SequenceEqual(expected))
        {
            throw new ConcurrencyConflictException(nameof(LandedCostVoucher), voucher.Id);
        }
    }
}
