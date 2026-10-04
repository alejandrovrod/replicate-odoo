using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using Erp.Domain.Services;

namespace Erp.Application.Features.Assets.Commands;

/// <summary>
/// Executes <see cref="PostDueDepreciationsCommand"/> (Task 10.4, scenarios AS-02/AS-04): company
/// resolution, the due-line read (ordered by date), the frozen-period gate, per-line booking and
/// the FullyDepreciated transition - all inside ONE <c>IAssetsRepository</c> transaction, so a
/// rejected run writes zero rows (no Booked marks, no accumulated moves, no GL).
/// </summary>
/// <remarks>
/// <para>
/// ONE voucher per run (not per line): every booked line posts its Dr/Cr pair into a single
/// gapless <c>DEP-YYYY-NNNNN</c> voucher, mirroring the manufacture precedent (one voucher per
/// posting operation). Each GL line carries its schedule-line identity (<c>VoucherId</c> = the
/// line id, plus the asset code and due date in Remarks) so per-line traceability survives the
/// batching, and the AS-04 replay test stays simple: a second run finds zero Scheduled rows
/// and books nothing.
/// </para>
/// <para>
/// Skip-vs-fail verdicts (documented, deliberate): a non-Scheduled line (already Booked or
/// Cancelled) is SKIPPED with its identity + reason (AS-04 replay safety - never double
/// expense, never fail the batch); a Scheduled line held by a non-Capitalized asset is a data
/// anomaly (lines exist only after capitalization) and is likewise SKIPPED with a reason -
/// failing the whole batch on one corrupt row would block every healthy asset. A frozen
/// ScheduleDate is instead an operator problem (back-dated postings are forbidden, spec AC-04)
/// and FAILS the whole run with zero writes, as does a tampered GL link
/// (<c>invalid_gl_account</c>), a breached salvage floor (<c>depreciated_past_salvage</c>) or a
/// RowVersion race (<c>concurrency_conflict</c>, spec AS-06 - the whole run aborts, zero writes).
/// </para>
/// </remarks>
public sealed class PostDueDepreciationsCommandHandler
    : ICommandHandler<PostDueDepreciationsCommand, Result<DepreciationRunDto>>
{
    private const string VoucherType = "Asset";
    private const string VoucherPrefix = "DEP";

    private readonly ICompanyRepository _companies;
    private readonly IAccountRepository _accounts;
    private readonly IAssetsRepository _assets;

    public PostDueDepreciationsCommandHandler(
        ICompanyRepository companies,
        IAccountRepository accounts,
        IAssetsRepository assets)
    {
        _companies = companies;
        _accounts = accounts;
        _assets = assets;
    }

    public async Task<Result<DepreciationRunDto>> HandleAsync(
        PostDueDepreciationsCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _assets.ExecuteInTransactionAsync(async token =>
            {
                var company = await _companies.GetByIdAsync(command.CompanyId, token)
                    ?? throw new AssetValidationException(
                        AssetErrorCodes.CompanyNotFound,
                        $"Company '{command.CompanyId}' was not found in this tenant.");

                var due = await _assets.GetDueSchedulesAsync(command.CompanyId, command.AsOfDate, token);

                if (due.Count == 0)
                {
                    return Result<DepreciationRunDto>.Success(new DepreciationRunDto(
                        VoucherNo: null, BookedCount: 0, TotalBooked: 0m,
                        Skipped: Array.Empty<DepreciationSkipDto>()));
                }

                // tasks.md 2.2 / spec AC-04: hard fiscal period lock - checked for EVERY due line
                // BEFORE a single GLEntry is built or a single status flips, so a run touching a
                // frozen date fails with ZERO writes (all-or-nothing).
                foreach (var line in due)
                {
                    company.EnsurePostingDateUnlocked(line.ScheduleDate);
                }

                var booked = new List<BookedLine>(due.Count);
                var skipped = new List<DepreciationSkipDto>();

                foreach (var line in due)
                {
                    // AS-04 replay-safety core: already-Booked (IsBooked = 1 equivalent) and
                    // Cancelled rows are skipped with their identity - never re-expensed.
                    if (line.Status != AssetScheduleStatus.Scheduled)
                    {
                        skipped.Add(new DepreciationSkipDto(
                            line.Id, line.AssetId,
                            line.Status == AssetScheduleStatus.Booked
                                ? "already_booked"
                                : "line_cancelled"));
                        continue;
                    }

                    var asset = await _assets.GetAssetByIdAsync(line.AssetId, token);

                    // Company mismatch must not leak across companies; a missing holder is corrupt
                    // data. Both are anomalies, not rejections: skip with a reason, never fail
                    // the batch.
                    if (asset is null)
                    {
                        skipped.Add(new DepreciationSkipDto(line.Id, line.AssetId, "asset_not_found"));
                        continue;
                    }

                    if (asset.CompanyId != command.CompanyId)
                    {
                        skipped.Add(new DepreciationSkipDto(line.Id, line.AssetId, "asset_company_mismatch"));
                        continue;
                    }

                    // Lines exist only after capitalization: a Scheduled line on a
                    // non-Capitalized holder means the data is corrupt. Skip + report, never fail
                    // the healthy assets sharing the run.
                    if (asset.Status != AssetStatus.Capitalized)
                    {
                        skipped.Add(new DepreciationSkipDto(
                            line.Id, line.AssetId, $"unexpected_asset_status_{asset.Status.ToString().ToLowerInvariant()}"));
                        continue;
                    }

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
                            $"Asset category '{category.CategoryName}' is inactive and its depreciation cannot be posted.");
                    }

                    // Accounts were vetted at capitalization; re-resolve cheaply and fail LOUDLY on
                    // tampering (a deactivated or grouped link after the fact blocks the run).
                    var expenseAccount = await AssetAccountGuards.RequirePostableAccountAsync(
                        _accounts, category.DepreciationExpenseAccountId, company.Id,
                        "depreciation expense account", token);
                    var accumAccount = await AssetAccountGuards.RequirePostableAccountAsync(
                        _accounts, category.AccumulatedDepreciationAccountId, company.Id,
                        "accumulated depreciation account", token);

                    // AS-01 salvage floor: accumulated must never exceed gross − salvage. A breach
                    // means the schedule drifted from its engine - abort the run, zero writes.
                    var depreciableBase = Round4(asset.GrossPurchaseAmount - asset.SalvageValue);
                    var newAccumulated = Round4(asset.AccumulatedDepreciation + line.DepreciationAmount);
                    if (newAccumulated - depreciableBase > 0m)
                    {
                        throw new AssetValidationException(
                            AssetErrorCodes.DepreciatedPastSalvage,
                            $"Booking line '{line.Id}' of asset '{asset.AssetCode}' would depreciate "
                            + $"{newAccumulated:0.####} past the depreciable base {depreciableBase:0.####} "
                            + $"(gross {asset.GrossPurchaseAmount:0.####} − salvage {asset.SalvageValue:0.####}).");
                    }

                    asset.AccumulatedDepreciation = newAccumulated;
                    line.Status = AssetScheduleStatus.Booked;
                    booked.Add(new BookedLine(asset, line, expenseAccount, accumAccount));
                }

                if (booked.Count == 0)
                {
                    return Result<DepreciationRunDto>.Success(new DepreciationRunDto(
                        VoucherNo: null, BookedCount: 0, TotalBooked: 0m, Skipped: skipped));
                }

                var voucherNo = await _assets.NextVoucherNumberAsync(
                    company.Id, VoucherPrefix, command.AsOfDate.Year, token);

                var glLines = new List<GLEntry>(booked.Count * 2);
                foreach (var entry in booked)
                {
                    var amount = Round4(entry.Line.DepreciationAmount);
                    glLines.Add(NewGlLine(company.Id, entry.Asset, entry.Line, entry.ExpenseAccount, voucherNo, debit: amount, credit: 0m));
                    glLines.Add(NewGlLine(company.Id, entry.Asset, entry.Line, entry.AccumAccount, voucherNo, debit: 0m, credit: amount));
                }

                // Constitution III.1: the batch voucher balances BEFORE anything is saved.
                DoubleEntryGuard.EnsureBalanced(glLines);

                foreach (var entry in booked)
                {
                    await _assets.UpdateAssetAsync(entry.Asset, token);
                    await _assets.UpdateScheduleAsync(entry.Line, token);
                }

                await _assets.AddGlEntriesAsync(glLines, token);

                // FullyDepreciated transition: no Scheduled lines remain AND the accumulated total
                // rests exactly on the depreciable base (NBV == salvage).
                var touchedAssets = booked.Select(b => b.Asset).DistinctBy(a => a.Id).ToList();
                foreach (var asset in touchedAssets)
                {
                    var remaining = await _assets.GetSchedulesByAssetAsync(asset.Id, token);
                    if (remaining.Any(l => l.Status == AssetScheduleStatus.Scheduled))
                    {
                        continue;
                    }

                    var depreciableBase = Round4(asset.GrossPurchaseAmount - asset.SalvageValue);
                    if (Round4(asset.AccumulatedDepreciation) == depreciableBase)
                    {
                        asset.MarkFullyDepreciated();
                        await _assets.UpdateAssetAsync(asset, token);
                    }
                }

                var total = Round4(booked.Sum(b => b.Line.DepreciationAmount));
                return Result<DepreciationRunDto>.Success(new DepreciationRunDto(
                    VoucherNo: voucherNo, BookedCount: booked.Count, TotalBooked: total, Skipped: skipped));
            }, cancellationToken);
        }
        catch (AssetValidationException ex)
        {
            return Result<DepreciationRunDto>.Failure(ex.Code, ex.Message);
        }
        catch (FiscalPeriodLockedException ex)
        {
            return Result<DepreciationRunDto>.Failure(ex.Code, ex.Message);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Result<DepreciationRunDto>.Failure(ex.Code, ex.Message);
        }
    }

    private static GLEntry NewGlLine(
        Guid companyId,
        Asset asset,
        AssetDepreciationSchedule line,
        Account account,
        string voucherNo,
        decimal debit,
        decimal credit) =>
        new()
        {
            CompanyId = companyId,
            PostingDate = line.ScheduleDate,
            AccountId = account.Id,
            Account = account,
            Debit = debit,
            Credit = credit,
            DebitInAccountCurrency = debit,
            CreditInAccountCurrency = credit,
            AccountCurrency = account.Currency,
            VoucherType = VoucherType,
            VoucherNo = voucherNo,
            VoucherId = line.Id,
            PartyType = null,
            PartyId = null,
            CostCenterId = null,
            IsCancelled = false,
            Remarks = $"{VoucherType} depreciation: {asset.AssetCode} due {line.ScheduleDate:yyyy-MM-dd}",
        };

    private static decimal Round4(decimal value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);

    /// <summary>
    /// One validated booking: the asset mutation, its line and the two vetted GL accounts travel
    /// together so the GL build never re-resolves what the validation pass already proved.
    /// </summary>
    private sealed record BookedLine(
        Asset Asset,
        AssetDepreciationSchedule Line,
        Account ExpenseAccount,
        Account AccumAccount);
}
