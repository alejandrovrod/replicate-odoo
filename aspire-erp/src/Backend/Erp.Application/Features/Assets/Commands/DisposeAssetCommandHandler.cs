using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using Erp.Domain.Services;

namespace Erp.Application.Features.Assets.Commands;

/// <summary>
/// Executes <see cref="DisposeAssetCommand"/> (Task 10.5, scenarios AS-03/AS-05): asset +
/// company + category resolution, the terminal-state gate, the frozen-period gate, the
/// proceeds-shape gate (cash-via-bank or scrap, never mixed), the gain/loss account
/// requirement, the balanced AS-03 voucher (Constitution III.1 via
/// <see cref="DoubleEntryGuard"/>), the future-line cancellation and the Sold/Scrapped
/// transition - all inside ONE <c>IAssetsRepository</c> transaction, so a rejected disposal
/// writes zero rows.
/// </summary>
/// <remarks>
/// Zero-write proofs: every gate throws BEFORE the first mutation (status transition, line
/// cancellation, GL add), and the try wraps the whole transaction callback, so the ambient
/// transaction never commits on ANY rejection path - including the RowVersion race
/// (<c>concurrency_conflict</c>, spec AS-06 groundwork).
/// </remarks>
public sealed class DisposeAssetCommandHandler
    : ICommandHandler<DisposeAssetCommand, Result<AssetDisposalDto>>
{
    private const string VoucherType = "Asset";
    private const string VoucherPrefix = "DSP";

    private readonly ICompanyRepository _companies;
    private readonly IAccountRepository _accounts;
    private readonly IBankRepository _banks;
    private readonly IAssetsRepository _assets;

    public DisposeAssetCommandHandler(
        ICompanyRepository companies,
        IAccountRepository accounts,
        IBankRepository banks,
        IAssetsRepository assets)
    {
        _companies = companies;
        _accounts = accounts;
        _banks = banks;
        _assets = assets;
    }

    public async Task<Result<AssetDisposalDto>> HandleAsync(
        DisposeAssetCommand command,
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

                // Terminal-state gate (covers Draft/Submitted, double disposal and disposal of an
                // already-exited asset): only live, capitalized assets may exit.
                if (asset.Status is not (AssetStatus.Capitalized or AssetStatus.FullyDepreciated))
                {
                    throw new AssetValidationException(
                        AssetErrorCodes.InvalidStatusTransition,
                        $"Only a Capitalized or FullyDepreciated asset can be disposed; asset '{asset.AssetCode}' is '{asset.Status}'.");
                }

                var company = await _companies.GetByIdAsync(command.CompanyId, token)
                    ?? throw new AssetValidationException(
                        AssetErrorCodes.CompanyNotFound,
                        $"Company '{command.CompanyId}' was not found in this tenant.");

                // tasks.md 2.2 / spec AC-04: hard fiscal period lock - before a single GLEntry or
                // status flip, so a back-dated disposal modifies ZERO data.
                company.EnsurePostingDateUnlocked(command.DisposalDate);

                // Proceeds-shape gate: cash-via-bank sales and $0 scraps only, never mixed. A
                // negative proceeds figure is nonsense in both shapes.
                if (command.ProceedsAmount < 0)
                {
                    throw new AssetValidationException(
                        AssetErrorCodes.InvalidProceeds,
                        $"Proceeds amount must not be negative (received {command.ProceedsAmount:0.####}).");
                }

                if (command.ProceedsAmount > 0 && command.ProceedsBankAccountId is null)
                {
                    throw new AssetValidationException(
                        AssetErrorCodes.InvalidProceeds,
                        "A sale with proceeds requires the bank account receiving the cash "
                        + "(credit-sale disposal via Accounts Receivable is out of scope).");
                }

                if (command.ProceedsAmount == 0 && command.ProceedsBankAccountId is not null)
                {
                    throw new AssetValidationException(
                        AssetErrorCodes.InvalidProceeds,
                        "A $0 scrap carries no proceeds, so no bank account may be attached to it.");
                }

                Account? bankGlAccount = null;
                if (command.ProceedsBankAccountId is { } bankAccountId)
                {
                    var bankAccount = await _banks.GetAccountByIdAsync(bankAccountId, token)
                        ?? throw new AssetValidationException(
                            AssetErrorCodes.BankAccountNotFound,
                            $"Bank account '{bankAccountId}' was not found in this tenant.");

                    if (bankAccount.CompanyId != company.Id)
                    {
                        throw new AssetValidationException(
                            AssetErrorCodes.BankAccountNotFound,
                            $"Bank account '{bankAccount.AccountName}' does not belong to company '{company.Id}'.");
                    }

                    // The proceeds debit lands on the bank's GL account: reuse the leaf guard so a
                    // grouped/inactive/foreign ledger link fails loudly instead of mis-posting.
                    bankGlAccount = await AssetAccountGuards.RequirePostableAccountAsync(
                        _accounts, bankAccount.GLAccountId, company.Id,
                        $"bank account '{bankAccount.AccountName}' GL account", token);
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
                        $"Asset category '{category.CategoryName}' is inactive and its assets cannot be disposed.");
                }

                // Resolve the ledger links BEFORE any write (Constitution III.3).
                var fixedAccount = await AssetAccountGuards.RequirePostableAccountAsync(
                    _accounts, category.FixedAssetAccountId, company.Id, "fixed asset account", token);
                var accumAccount = await AssetAccountGuards.RequirePostableAccountAsync(
                    _accounts, category.AccumulatedDepreciationAccountId, company.Id,
                    "accumulated depreciation account", token);

                var gross = Round4(asset.GrossPurchaseAmount);
                var accrued = Round4(asset.AccumulatedDepreciation);
                var nbv = Round4(gross - accrued);
                var proceeds = Round4(command.ProceedsAmount);
                var variance = Round4(proceeds - nbv);

                Account? gainAccount = null;
                Account? lossAccount = null;
                if (variance > 0)
                {
                    gainAccount = category.GainOnDisposalAccountId is { } gainId
                        ? await AssetAccountGuards.RequirePostableAccountAsync(
                            _accounts, gainId, company.Id, "gain on disposal account", token)
                        : throw new AssetValidationException(
                            AssetErrorCodes.MissingGainLossAccount,
                            $"Asset category '{category.CategoryName}' has no gain-on-disposal account, "
                            + $"but disposing asset '{asset.AssetCode}' realizes a gain of {variance:0.####}.");
                }
                else if (variance < 0)
                {
                    lossAccount = category.LossOnDisposalAccountId is { } lossId
                        ? await AssetAccountGuards.RequirePostableAccountAsync(
                            _accounts, lossId, company.Id, "loss on disposal account", token)
                        : throw new AssetValidationException(
                            AssetErrorCodes.MissingGainLossAccount,
                            $"Asset category '{category.CategoryName}' has no loss-on-disposal account, "
                            + $"but disposing asset '{asset.AssetCode}' realizes a loss of {-variance:0.####}.");
                }

                // ELSE break-even (variance == 0): neither line is emitted - the voucher still
                // balances (Dr Accum + Dr Bank == Cr Fixed by construction).

                var voucherNo = await _assets.NextVoucherNumberAsync(
                    company.Id, VoucherPrefix, command.DisposalDate.Year, token);

                // Spec AS-03: Dr Accum(accrued) + Dr Bank(proceeds, when sold) + Dr Loss/Cr Gain +
                // Cr Fixed(gross). Zero proceeds (scrap) degrade to Dr Accum + Dr Loss / Cr Fixed.
                var glLines = new List<GLEntry>(4)
                {
                    NewGlLine(company.Id, asset, accumAccount, command.DisposalDate, voucherNo, asset.Id, debit: accrued, credit: 0m),
                    NewGlLine(company.Id, asset, fixedAccount, command.DisposalDate, voucherNo, asset.Id, debit: 0m, credit: gross),
                };

                if (bankGlAccount is not null && proceeds > 0)
                {
                    glLines.Add(NewGlLine(company.Id, asset, bankGlAccount, command.DisposalDate, voucherNo, asset.Id, debit: proceeds, credit: 0m));
                }

                if (gainAccount is not null && variance > 0)
                {
                    glLines.Add(NewGlLine(company.Id, asset, gainAccount, command.DisposalDate, voucherNo, asset.Id, debit: 0m, credit: variance));
                }
                else if (lossAccount is not null && variance < 0)
                {
                    glLines.Add(NewGlLine(company.Id, asset, lossAccount, command.DisposalDate, voucherNo, asset.Id, debit: -variance, credit: 0m));
                }

                // Constitution III.1: balance must hold to four decimals BEFORE anything is saved.
                DoubleEntryGuard.EnsureBalanced(glLines);

                // AS-05: remaining future Scheduled lines are cancelled; Booked history is immutable
                // and stays exactly as posted.
                var schedules = await _assets.GetSchedulesByAssetAsync(asset.Id, token);
                var cancelledCount = 0;
                foreach (var line in schedules.Where(l => l.Status == AssetScheduleStatus.Scheduled))
                {
                    line.Status = AssetScheduleStatus.Cancelled;
                    await _assets.UpdateScheduleAsync(line, token);
                    cancelledCount++;
                }

                var terminal = proceeds > 0 ? AssetStatus.Sold : AssetStatus.Scrapped;
                asset.Dispose(terminal, command.DisposalDate);
                asset.DisposalProceedsAmount = proceeds;
                asset.DisposalVoucherNo = voucherNo;
                await _assets.UpdateAssetAsync(asset, token);

                await _assets.AddGlEntriesAsync(glLines, token);

                return Result<AssetDisposalDto>.Success(new AssetDisposalDto(
                    AssetDto.Build(asset), voucherNo, proceeds, variance, cancelledCount));
            }, cancellationToken);
        }
        catch (AssetValidationException ex)
        {
            return Result<AssetDisposalDto>.Failure(ex.Code, ex.Message);
        }
        catch (FiscalPeriodLockedException ex)
        {
            return Result<AssetDisposalDto>.Failure(ex.Code, ex.Message);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Result<AssetDisposalDto>.Failure(ex.Code, ex.Message);
        }
    }

    private static GLEntry NewGlLine(
        Guid companyId,
        Asset asset,
        Account account,
        DateOnly disposalDate,
        string voucherNo,
        Guid voucherId,
        decimal debit,
        decimal credit) =>
        new()
        {
            CompanyId = companyId,
            PostingDate = disposalDate,
            AccountId = account.Id,
            Account = account,
            Debit = debit,
            Credit = credit,
            DebitInAccountCurrency = debit,
            CreditInAccountCurrency = credit,
            AccountCurrency = account.Currency?.Code ?? "USD",
            VoucherType = VoucherType,
            VoucherNo = voucherNo,
            VoucherId = voucherId,
            PartyType = null,
            PartyId = null,
            CostCenterId = null,
            IsCancelled = false,
            Remarks = $"{VoucherType} disposal: {asset.AssetName} ({asset.AssetCode})",
        };

    private static decimal Round4(decimal value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);
}
