using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using Erp.Domain.Services;

namespace Erp.Application.Features.Assets.Commands;

/// <summary>
/// Executes <see cref="CancelDisposeAssetCommand"/> (Task 10.6, spec AS-05 reversal):
/// undoes an asset disposal by reversing the disposal GL lines, reopening cancelled
/// schedule lines, restoring AccumulatedDepreciation to its pre-disposal value,
/// clearing DisposalDate, and restoring the asset to Capitalized (or FullyDepreciated
/// if no Scheduled lines remain). All operations occur inside ONE ambient transaction
/// so a rejection writes ZERO rows.
/// </summary>
public sealed class CancelDisposeAssetCommandHandler
    : ICommandHandler<CancelDisposeAssetCommand, Result<AssetDisposalReversalDto>>
{
    private readonly ICompanyRepository _companies;
    private readonly IAccountRepository _accounts;
    private readonly IAssetsRepository _assets;

    public CancelDisposeAssetCommandHandler(
        ICompanyRepository companies,
        IAccountRepository accounts,
        IAssetsRepository assets)
    {
        _companies = companies;
        _accounts = accounts;
        _assets = assets;
    }

    public async Task<Result<AssetDisposalReversalDto>> HandleAsync(
        CancelDisposeAssetCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _assets.ExecuteInTransactionAsync(async token =>
            {
                var asset = await _assets.GetAssetByIdAsync(command.AssetId, token)
                    ?? throw new AssetValidationException(
                        AssetErrorCodes.AssetNotFound,
                        $"Asset '{command.AssetId}' was not found in this tenant.");

                if (asset.CompanyId != command.CompanyId)
                {
                    throw new AssetValidationException(
                        AssetErrorCodes.AssetNotFound,
                        $"Asset '{asset.AssetCode}' does not belong to company '{command.CompanyId}'.");
                }

                // Get the original disposal GL lines to reverse them.
                var disposalGlLines = await _assets.GetDisposalGlEntriesAsync(asset.Id, token);

                if (disposalGlLines.Count == 0)
                {
                    throw new AssetValidationException(
                        AssetErrorCodes.AssetNotDisposed,
                        $"Asset '{asset.AssetCode}' has not been disposed and cannot be reversed.");
                }

                // Check if already reversed.
                var alreadyReversed = await _assets.HasDisposalReversalAsync(asset.Id, token);
                if (alreadyReversed)
                {
                    throw new AssetValidationException(
                        AssetErrorCodes.AlreadyReversed,
                        $"Asset '{asset.AssetCode}' has already had its disposal cancelled.");
                }

                // Get reversal voucher number and company info early.
                var postingDate = command.PostingDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
                var company = await _companies.GetByIdAsync(command.CompanyId, token)
                    ?? throw new AssetValidationException(
                        AssetErrorCodes.CompanyNotFound,
                        $"Company '{command.CompanyId}' was not found in this tenant.");
                
                company.EnsurePostingDateUnlocked(postingDate);
                // R-13 FC-04: cancelling into a closed year is refused — the close is immutable.
                await _companies.EnsurePostingDateInOpenYearAsync(company.Id, postingDate, token);

                var reversalVoucherNo = await _assets.NextReversalVoucherNumberAsync(
                    company.Id, "RDS", postingDate.Year, token);

                // Build reversal GL lines by swapping debit/credit of the original disposal lines.
                var reversalGlLines = disposalGlLines.Select(original => NewReversalGlLine(
                    company.Id, asset, original, postingDate, reversalVoucherNo)).ToList();

                DoubleEntryGuard.EnsureBalanced(reversalGlLines);

                // Reopen cancelled schedule lines.
                var schedules = await _assets.GetSchedulesByAssetAsync(asset.Id, token);
                var reopenedLinesCount = 0;
                foreach (var line in schedules.Where(l => l.Status == AssetScheduleStatus.Cancelled))
                {
                    line.Status = AssetScheduleStatus.Scheduled;
                    await _assets.UpdateScheduleAsync(line, token);
                    reopenedLinesCount++;
                }

                // Restore AccumulatedDepreciation: find the disposal line that credited AccumDep.
                var categoryAsync = await _assets.GetCategoryByIdAsync(asset.AssetCategoryId, token);
                var accruedCreditLine = disposalGlLines
                    .FirstOrDefault(g => g.AccountId == categoryAsync.AccumulatedDepreciationAccountId && g.Credit > 0);
                var accruedAmount = accruedCreditLine?.Credit ?? 0m;

                if (accruedAmount > 0)
                {
                    asset.AccumulatedDepreciation = Round4(asset.AccumulatedDepreciation - accruedAmount);
                }



                // Determine the post-reversal status.
                var remainingScheduled = await _assets.GetSchedulesByAssetAsync(asset.Id, token);
                var hasScheduled = remainingScheduled.Any(l => l.Status == AssetScheduleStatus.Scheduled);
                var depreciableBase = Round4(asset.GrossPurchaseAmount - asset.SalvageValue);
                var newStatus = !remainingScheduled.Any(l => l.Status == AssetScheduleStatus.Scheduled)
                    && asset.AccumulatedDepreciation >= asset.GrossPurchaseAmount - asset.SalvageValue
                    ? AssetStatus.FullyDepreciated
                    : AssetStatus.Capitalized;

                // Cancel the disposal: clear DisposalDate, reset status, clear disposal proceeds.
                asset.UndoDisposal(newStatus);
                asset.DisposalDate = null;
                asset.DisposalProceedsAmount = 0m;
                asset.DisposalVoucherNo = string.Empty;
                await _assets.UpdateAssetAsync(asset, token);

                // Post the reversal GL lines.
                await _assets.AddGlEntriesAsync(reversalGlLines, token);

                return Result<AssetDisposalReversalDto>.Success(new AssetDisposalReversalDto(
                    AssetDto.Build(asset), 
                    reversalVoucherNo,
                    reopenedLinesCount));
            }, cancellationToken);
        }
        catch (AssetValidationException ex)
        {
            return Result<AssetDisposalReversalDto>.Failure(ex.Code, ex.Message);
        }
        catch (FiscalPeriodLockedException ex)
        {
            return Result<AssetDisposalReversalDto>.Failure(ex.Code, ex.Message);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Result<AssetDisposalReversalDto>.Failure(ex.Code, ex.Message);
        }
    }

    private static GLEntry NewReversalGlLine(
        Guid companyId,
        Asset asset,
        GLEntry original,
        DateOnly postingDate,
        string voucherNo) =>
        new()
        {
            CompanyId = companyId,
            PostingDate = postingDate,
            AccountId = original.AccountId,
            Account = original.Account,
            Debit = original.Credit,
            Credit = original.Debit,
            DebitInAccountCurrency = original.CreditInAccountCurrency,
            CreditInAccountCurrency = original.DebitInAccountCurrency,
            AccountCurrency = original.AccountCurrency,
            VoucherType = "Asset",
            VoucherNo = voucherNo,
            VoucherId = asset.Id,
            PartyType = null,
            PartyId = null,
            CostCenterId = null,
            IsCancelled = true,
            Remarks = $"Reversal of disposal {original.VoucherNo}: {original.Remarks}",
        };

    private static decimal Round4(decimal value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);
}