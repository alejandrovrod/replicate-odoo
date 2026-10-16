using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using Erp.Domain.Services;

namespace Erp.Application.Features.LandedCost.Commands;

/// <summary>
/// Submits a Draft landed cost voucher (Fase 4, LCV-G3): gapless LCV number, per
/// item-warehouse inventory debits against each charge's bridge/expense account (balanced
/// by construction), and zero-quantity Kardex revaluation rows that lift the book value
/// without moving physical units.
/// </summary>
public sealed class SubmitLandedCostVoucherCommandHandler
    : ICommandHandler<SubmitLandedCostVoucherCommand, Result<LandedCostVoucherDto>>
{
    private const string VoucherType = "LandedCostVoucher";
    private const string VoucherPrefix = "LCV";

    private readonly ILandedCostVoucherRepository _vouchers;
    private readonly IPurchaseRepository _purchases;
    private readonly ICompanyRepository _companies;
    private readonly IAccountRepository _accounts;
    private readonly IWarehouseRepository _warehouses;
    private readonly IItemRepository _items;
    private readonly IStockRepository _stock;

    public SubmitLandedCostVoucherCommandHandler(
        ILandedCostVoucherRepository vouchers,
        IPurchaseRepository purchases,
        ICompanyRepository companies,
        IAccountRepository accounts,
        IWarehouseRepository warehouses,
        IItemRepository items,
        IStockRepository stock)
    {
        _vouchers = vouchers;
        _purchases = purchases;
        _companies = companies;
        _accounts = accounts;
        _warehouses = warehouses;
        _items = items;
        _stock = stock;
    }

    public async Task<Result<LandedCostVoucherDto>> HandleAsync(
        SubmitLandedCostVoucherCommand command, CancellationToken cancellationToken = default)
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
                LandedCostValidator.EnsureSubmittable(voucher.Status, voucher.VoucherNo);

                var company = await _companies.GetByIdAsync(voucher.CompanyId, token)
                    ?? throw new LandedCostValidationException(
                        LandedCostErrorCodes.LandedCostVoucherNotFound,
                        $"Company '{voucher.CompanyId}' was not found in this tenant.");

                company.EnsurePostingDateUnlocked(voucher.PostingDate);
                await _companies.EnsurePostingDateInOpenYearAsync(company.Id, voucher.PostingDate, token);

                // Money gates BEFORE any write: every charge account re-resolves (chart edits
                // between Draft and submit must not smuggle postings into wrong accounts).
                var expenseAccounts = new Dictionary<Guid, Account>();
                foreach (var charge in voucher.Charges)
                {
                    expenseAccounts[charge.Id] = await RequirePostingLeafAsync(
                        charge.ExpenseAccountId, company.Id, charge.Description, token);
                }

                // Resolve receipt lines (warehouse + batch provenance per apportioned line).
                var receiptLinesById = new Dictionary<Guid, (PurchaseReceipt Receipt, PurchaseReceiptLine Line)>();
                foreach (var receiptId in voucher.Receipts.Select(r => r.PurchaseReceiptId).Distinct())
                {
                    var receipt = await _purchases.GetReceiptByIdAsync(receiptId, token)
                        ?? throw new LandedCostValidationException(
                            LandedCostErrorCodes.PurchaseReceiptNotSubmitted,
                            $"Purchase receipt '{receiptId}' was not found in this tenant.");
                    foreach (var line in receipt.Lines)
                    {
                        receiptLinesById[line.Id] = (receipt, line);
                    }
                }

                // Group value deltas per (warehouse → stock account): one inventory debit leg
                // per warehouse, one credit leg per charge.
                var deltaByWarehouse = new Dictionary<Guid, decimal>();
                var warehouseById = new Dictionary<Guid, Warehouse>();
                var ledger = new List<StockLedgerEntry>(voucher.Items.Count);

                foreach (var apportioned in voucher.Items)
                {
                    if (!receiptLinesById.TryGetValue(apportioned.PurchaseReceiptLineId, out var source))
                    {
                        throw new LandedCostValidationException(
                            LandedCostErrorCodes.PurchaseReceiptNotSubmitted,
                            "A receipt line apportioned by this voucher no longer exists.");
                    }

                    var (receipt, receiptLine) = source;
                    if (!warehouseById.TryGetValue(receipt.WarehouseId, out var warehouse))
                    {
                        warehouse = await _warehouses.GetByIdAsync(receipt.WarehouseId, token)
                            ?? throw new LandedCostValidationException(
                                LandedCostErrorCodes.PurchaseReceiptNotSubmitted,
                                $"Warehouse '{receipt.WarehouseId}' was not found in this tenant.");
                        warehouseById[receipt.WarehouseId] = warehouse;
                    }

                    deltaByWarehouse[warehouse.Id] = deltaByWarehouse.TryGetValue(warehouse.Id, out var acc)
                        ? acc + apportioned.ApplicableCharges
                        : apportioned.ApplicableCharges;

                    ledger.Add(new StockLedgerEntry
                    {
                        Id = Guid.NewGuid(),
                        TenantId = voucher.TenantId,
                        ItemId = apportioned.ItemId,
                        WarehouseId = warehouse.Id,
                        BatchId = receiptLine.BatchId,
                        PostingDate = voucher.PostingDate,
                        QtyChange = 0m,
                        ValuationRate = apportioned.NewRate,
                        Amount = apportioned.ApplicableCharges,
                        CreatedAt = DateTimeOffset.UtcNow,
                    });
                }

                // No-stock gate: every (item, warehouse) pair absorbing charges must still hold
                // open layers, otherwise the revaluation has nothing to attach to (sold out).
                foreach (var group in voucher.Items.GroupBy(i => new { i.ItemId, WarehouseId = WarehouseOf(i, receiptLinesById) }))
                {
                    var layers = FifoValuation.BuildLayers(
                        await _stock.GetFifoLayersAsync(group.Key.ItemId, group.Key.WarehouseId, voucher.PostingDate, token));
                    if (!layers.Any(l => l.RemainingQty > 0))
                    {
                        throw new LandedCostValidationException(
                            LandedCostErrorCodes.NoStockToRevalue,
                            "An item absorbed by this voucher holds no open stock layers: landed costs "
                            + "attach to on-hand stock, not to already-sold quantities.");
                    }
                }

                var glLines = new List<GLEntry>(deltaByWarehouse.Count + voucher.Charges.Count);
                foreach (var (warehouseId, delta) in deltaByWarehouse)
                {
                    if (delta == 0m)
                    {
                        continue;
                    }

                    var stockAccount = await RequireStockAccountAsync(warehouseById[warehouseId], company.Id, token);
                    glLines.Add(NewGlLine(
                        company, voucher, stockAccount, debit: delta, credit: 0m,
                        $"Landed cost: inventory uplift {voucher.VoucherNo}"));
                }

                foreach (var charge in voucher.Charges)
                {
                    var account = expenseAccounts[charge.Id];
                    glLines.Add(NewGlLine(
                        company, voucher, account, debit: 0m, credit: charge.Amount,
                        $"Landed cost: {charge.Description}"));
                }

                // Constitution III.1: exact balance BEFORE anything is saved (the apportionment
                // engine distributes to the cent, so this proves rather than assumes).
                DoubleEntryGuard.EnsureBalanced(glLines);

                voucher.VoucherNo = await _stock.NextVoucherNumberAsync(
                    company.Id, VoucherPrefix, voucher.PostingDate.Year, token);
                voucher.Status = DocumentStatus.Submitted;

                foreach (var entry in ledger)
                {
                    entry.VoucherType = VoucherType;
                    entry.VoucherNo = voucher.VoucherNo;
                }

                foreach (var glLine in glLines)
                {
                    glLine.VoucherNo = voucher.VoucherNo;
                    glLine.VoucherId = voucher.Id;
                }

                await _vouchers.UpdateAsync(voucher, token);
                await _stock.AddLedgerEntriesAsync(ledger, token);
                await _stock.AddGlEntriesAsync(glLines, token);

                return Result<LandedCostVoucherDto>.Success(await BuildDtoAsync(voucher, token));
            }, cancellationToken);
        }
        catch (LandedCostValidationException ex)
        {
            return Result<LandedCostVoucherDto>.Failure(ex.Code, ex.Message);
        }
        catch (StockValidationException ex)
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

    private static Guid WarehouseOf(
        LandedCostApportionmentItem item,
        IReadOnlyDictionary<Guid, (PurchaseReceipt Receipt, PurchaseReceiptLine Line)> receiptLinesById) =>
        receiptLinesById[item.PurchaseReceiptLineId].Receipt.WarehouseId;

    private async Task<LandedCostVoucherDto> BuildDtoAsync(LandedCostVoucher voucher, CancellationToken ct)
    {
        var itemIds = voucher.Items.Select(i => i.ItemId).Distinct().ToList();
        var itemsById = (await _items.GetByIdsAsync(itemIds, ct)).ToDictionary(i => i.Id);
        var receiptNumbers = new Dictionary<Guid, string>();
        foreach (var receiptId in voucher.Receipts.Select(r => r.PurchaseReceiptId).Distinct())
        {
            var receipt = await _purchases.GetReceiptByIdAsync(receiptId, ct);
            if (receipt is not null)
            {
                receiptNumbers[receiptId] = receipt.VoucherNo;
            }
        }

        foreach (var charge in voucher.Charges)
        {
            charge.ExpenseAccount = await _accounts.GetByIdAsync(charge.ExpenseAccountId, ct);
        }

        return LandedCostVoucherDto.Build(voucher, itemsById, receiptNumbers);
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

    private async Task<Account> RequirePostingLeafAsync(
        Guid accountId, Guid companyId, string description, CancellationToken ct)
    {
        var account = await _accounts.GetByIdAsync(accountId, ct);
        if (account is null || account.CompanyId != companyId || !account.IsActive || account.IsGroup)
        {
            throw new LandedCostValidationException(
                LandedCostErrorCodes.ExpenseAccountRequired,
                $"Charge '{description}' must reference an active posting (leaf) account of this company.");
        }

        return account;
    }

    private async Task<Account> RequireStockAccountAsync(
        Warehouse warehouse, Guid companyId, CancellationToken ct)
    {
        var accountId = warehouse.AccountId ?? Guid.Empty;
        var account = accountId == Guid.Empty
            ? null
            : await _accounts.GetByIdAsync(accountId, ct);

        if (account is null || account.CompanyId != companyId || !account.IsActive || account.IsGroup)
        {
            throw new StockValidationException(
                StockErrorCodes.MissingStockAccount,
                $"Warehouse '{warehouse.WarehouseCode}' has no postable stock account linked.");
        }

        return account;
    }

    private static GLEntry NewGlLine(
        Company company,
        LandedCostVoucher voucher,
        Account account,
        decimal debit,
        decimal credit,
        string remarks) =>
        new()
        {
            TenantId = company.TenantId,
            CompanyId = company.Id,
            PostingDate = voucher.PostingDate,
            AccountId = account.Id,
            Account = account,
            Debit = Round4(debit),
            Credit = Round4(credit),
            DebitInAccountCurrency = Round4(debit),
            CreditInAccountCurrency = Round4(credit),
            AccountCurrency = account.Currency?.Code ?? "USD",
            VoucherType = VoucherType,
            VoucherNo = string.Empty,
            VoucherId = Guid.Empty,
            IsCancelled = false,
            Remarks = remarks,
            CreatedAt = DateTimeOffset.UtcNow,
        };

    private static decimal Round4(decimal value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);
}
