using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using Erp.Domain.Services;

namespace Erp.Application.Features.Assets.Commands;

/// <summary>
/// Executes <see cref="CapitalizeAssetCommand"/> (Task 10.2, scenario AS-01): asset + company +
/// category resolution, field guards (salvage &lt;= gross, positive periods), the CWIP-required
/// boundary, the frozen-period gate, the balanced Dr Fixed / Cr CWIP pair (Constitution III.1 via
/// <see cref="DoubleEntryGuard"/>), the Task 10.3 schedule generation and the Draft/Submitted -&gt;
/// Capitalized transition - all inside ONE <c>IAssetsRepository</c> transaction, so a rejected
/// capitalization writes zero rows (asset, schedule and GL stay untouched).
/// </summary>
/// <remarks>
/// Outright purchases without CWIP stay out (Task 10.2 boundary): a category with no CWIP link
/// fails with <c>missing_cwip_account</c> instead of inventing a clearing account.
/// </remarks>
public sealed class CapitalizeAssetCommandHandler
    : ICommandHandler<CapitalizeAssetCommand, Result<AssetCapitalizationDto>>
{
    private const string VoucherType = "Asset";

    private readonly ICompanyRepository _companies;
    private readonly IAccountRepository _accounts;
    private readonly IItemRepository _items;
    private readonly IAssetsRepository _assets;

    public CapitalizeAssetCommandHandler(
        ICompanyRepository companies,
        IAccountRepository accounts,
        IItemRepository items,
        IAssetsRepository assets)
    {
        _companies = companies;
        _accounts = accounts;
        _items = items;
        _assets = assets;
    }

    public async Task<Result<AssetCapitalizationDto>> HandleAsync(
        CapitalizeAssetCommand command,
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

                // Company mismatch is reported as NOT FOUND: the id must not leak across companies.
                if (asset.CompanyId != command.CompanyId)
                {
                    throw new AssetValidationException(
                        AssetErrorCodes.AssetNotFound,
                        $"Asset '{command.AssetId}' does not belong to company '{command.CompanyId}'.");
                }

                if (asset.Status is not (AssetStatus.Draft or AssetStatus.Submitted))
                {
                    throw new AssetValidationException(
                        AssetErrorCodes.InvalidStatusTransition,
                        $"Only a Draft or Submitted asset can be capitalized; asset '{asset.AssetCode}' is '{asset.Status}'.");
                }

                var company = await _companies.GetByIdAsync(command.CompanyId, token)
                    ?? throw new AssetValidationException(
                        AssetErrorCodes.CompanyNotFound,
                        $"Company '{command.CompanyId}' was not found in this tenant.");

                // tasks.md 2.2 / spec AC-04: hard fiscal period lock - before a single GLEntry or
                // schedule line is built, so a back-dated attempt modifies ZERO data.
                company.EnsurePostingDateUnlocked(command.CapitalizationDate);

                var category = await _assets.GetCategoryByIdAsync(asset.AssetCategoryId, token)
                    ?? throw new AssetValidationException(
                        AssetErrorCodes.CategoryNotFound,
                        $"Asset category '{asset.AssetCategoryId}' was not found in this tenant.");

                if (category.CompanyId != company.Id)
                {
                    throw new AssetValidationException(
                        AssetErrorCodes.CategoryNotFound,
                        $"Asset category '{category.CategoryName}' does not belong to company '{company.Id}'.");
                }

                if (!category.IsActive)
                {
                    throw new AssetValidationException(
                        AssetErrorCodes.InactiveCategory,
                        $"Asset category '{category.CategoryName}' is inactive and cannot capitalize assets.");
                }

                AssetValidator.EnsureValidGrossAmount(asset.GrossPurchaseAmount);
                AssetValidator.EnsureValidSalvageValue(asset.GrossPurchaseAmount, asset.SalvageValue);
                AssetValidator.EnsureValidDepreciationPeriods(
                    asset.TotalNumberOfDepreciations, asset.FrequencyInMonths);

                if (category.CwipAccountId is not { } cwipAccountId)
                {
                    throw new AssetValidationException(
                        AssetErrorCodes.MissingCwipAccount,
                        $"Asset category '{category.CategoryName}' has no CWIP account; outright purchases "
                        + "without CWIP cannot be capitalized (link a CWIP account first).");
                }

                _ = await _items.GetByIdAsync(asset.ItemId, token)
                    ?? throw new AssetValidationException(
                        AssetErrorCodes.ItemNotFound,
                        $"Item '{asset.ItemId}' was not found in this tenant.");

                // Resolve + sanity-check the GL accounts BEFORE any write (Constitution III.3).
                var fixedAssetAccount = await AssetAccountGuards.RequirePostableAccountAsync(
                    _accounts, category.FixedAssetAccountId, company.Id, "fixed asset account", token);
                var cwipAccount = await AssetAccountGuards.RequirePostableAccountAsync(
                    _accounts, cwipAccountId, company.Id, "CWIP account", token);

                if (string.IsNullOrWhiteSpace(asset.AssetCode))
                {
                    asset.AssetCode = await _assets.NextAssetCodeAsync(
                        company.Id, command.CapitalizationDate.Year, token);
                }

                // CWIP clearing voucher: Dr Fixed Asset / Cr CWIP for the gross purchase amount.
                var glLines = BuildCapitalizationGlLines(company.Id, asset, fixedAssetAccount, cwipAccount, command.CapitalizationDate);

                // Constitution III.1: balance must hold to four decimals BEFORE anything is saved.
                DoubleEntryGuard.EnsureBalanced(glLines);

                var planned = DepreciationScheduler.GenerateStraightLineSchedule(
                    asset.GrossPurchaseAmount,
                    asset.SalvageValue,
                    asset.TotalNumberOfDepreciations,
                    asset.FrequencyInMonths,
                    asset.AvailableForUseDate);

                var lines = new List<AssetDepreciationSchedule>(planned.Count);
                foreach (var line in planned)
                {
                    lines.Add(new AssetDepreciationSchedule
                    {
                        Id = Guid.NewGuid(),
                        AssetId = asset.Id,
                        ScheduleDate = line.ScheduleDate,
                        DepreciationAmount = line.DepreciationAmount,
                        AccumulatedDepreciationAfter = line.AccumulatedDepreciation,
                        Status = AssetScheduleStatus.Scheduled,
                    });
                }

                asset.Capitalize();

                await _assets.UpdateAssetAsync(asset, token);
                await _assets.AddScheduleRangeAsync(lines, token);
                await _assets.AddGlEntriesAsync(glLines, token);

                var lineDtos = lines
                    .OrderBy(l => l.ScheduleDate)
                    .Select(l => new AssetScheduleLineDto(
                        l.Id, l.ScheduleDate, l.DepreciationAmount, l.AccumulatedDepreciationAfter, l.Status))
                    .ToList();

                return Result<AssetCapitalizationDto>.Success(new AssetCapitalizationDto(
                    AssetDto.Build(asset),
                    lineDtos,
                    lineDtos.Count,
                    Round4(lineDtos.Sum(l => l.DepreciationAmount))));
            }, cancellationToken);
        }
        catch (AssetValidationException ex)
        {
            return Result<AssetCapitalizationDto>.Failure(ex.Code, ex.Message);
        }
        catch (FiscalPeriodLockedException ex)
        {
            return Result<AssetCapitalizationDto>.Failure(ex.Code, ex.Message);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Result<AssetCapitalizationDto>.Failure(ex.Code, ex.Message);
        }
    }

    private static List<GLEntry> BuildCapitalizationGlLines(
        Guid companyId,
        Asset asset,
        Account fixedAssetAccount,
        Account cwipAccount,
        DateOnly capitalizationDate)
    {
        var amount = Round4(asset.GrossPurchaseAmount);

        return new List<GLEntry>(2)
        {
            NewGlLine(companyId, asset, fixedAssetAccount, capitalizationDate, debit: amount, credit: 0m),
            NewGlLine(companyId, asset, cwipAccount, capitalizationDate, debit: 0m, credit: amount),
        };
    }

    private static GLEntry NewGlLine(
        Guid companyId,
        Asset asset,
        Account account,
        DateOnly capitalizationDate,
        decimal debit,
        decimal credit) =>
        new()
        {
            CompanyId = companyId,
            PostingDate = capitalizationDate,
            AccountId = account.Id,

            // Navigation kept populated so the API response can show account code + name without
            // a second round trip (EF fixes up the FK from the reference anyway).
            Account = account,
            Debit = debit,
            Credit = credit,

            // plan.md §2 account-currency pair: single-currency postings book the ledger amount
            // 1:1 and snapshot the account currency (same shape as the stock postings).
            DebitInAccountCurrency = debit,
            CreditInAccountCurrency = credit,
            AccountCurrency = account.Currency,

            VoucherType = VoucherType,
            VoucherNo = asset.AssetCode,
            VoucherId = asset.Id,
            PartyType = null,
            PartyId = null,
            CostCenterId = null,
            IsCancelled = false,
            Remarks = $"{VoucherType} capitalization: {asset.AssetName} ({asset.AssetCode})",
        };

    private static decimal Round4(decimal value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);
}
