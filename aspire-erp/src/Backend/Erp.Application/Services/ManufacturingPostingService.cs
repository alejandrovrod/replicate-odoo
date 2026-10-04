using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using Erp.Domain.Services;

namespace Erp.Application.Services;

/// <summary>
/// The manufacture posting engine of Task 9.4 (spec MF-03). Responsibilities, in order, all inside
/// one transaction provided by <see cref="IManufacturingRepository.ExecuteInTransactionAsync{T}"/>
/// (which joins the stock repository's ambient transaction - one shared scoped DbContext):
/// validation -&gt; frozen-period gate -&gt; FIFO consumption of every component from WIP (pure
/// Domain, range-locked) -&gt; operating-cost roll-up from the BOM's planned operations -&gt;
/// cost-engine unit rate -&gt; finished-goods receipt -&gt; balanced three-leg voucher -&gt;
/// gapless MF number -&gt; save + work-order completion (Constitution III.1/III.4).
/// </summary>
/// <remarks>
/// GL mapping reproduces the spec scenario verbatim: the finished-goods warehouse account is
/// debited with the total capitalized cost (MF-03: Dr 1330 $700), the WIP warehouse account is
/// credited with the FIFO-consumed raw cost (Cr 1320 $500), and the company's COGS/absorption
/// account is credited with the operating cost (Cr 5210 $200). Stock legs resolve through the
/// warehouses' linked accounts - the StockPostingService pattern - so the Kardex mirror holds;
/// the absorption leg resolves through Company.CogsAccountCode (decision D3 code-not-FK shape),
/// which the dev seed holds at 5210.
/// Planned-operations basis: operating cost scales the BOM's timed workstation steps by produced
/// quantity. Actual operator time tracking is JobCard scope (deferred) - the voucher absorbs the
/// engineered cost, exactly like the BOM roll-up.
/// </remarks>
public sealed class ManufacturingPostingService : IManufacturingPostingService
{
    private const string VoucherType = "StockEntry";
    private const string VoucherPrefix = "MF";

    private readonly ICompanyRepository _companies;
    private readonly IAccountRepository _accounts;
    private readonly IWarehouseRepository _warehouses;
    private readonly IItemRepository _items;
    private readonly IStockRepository _stock;
    private readonly IManufacturingRepository _manufacturing;

    public ManufacturingPostingService(
        ICompanyRepository companies,
        IAccountRepository accounts,
        IWarehouseRepository warehouses,
        IItemRepository items,
        IStockRepository stock,
        IManufacturingRepository manufacturing)
    {
        _companies = companies;
        _accounts = accounts;
        _warehouses = warehouses;
        _items = items;
        _stock = stock;
        _manufacturing = manufacturing;
    }

    public async Task<StockEntryPostingDto> CompleteAsync(
        WorkOrderCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // One transaction for the WHOLE posting: validation, FIFO, SLE, GL, voucher, save and
        // the work-order transition commit together or not at all.
        return await _manufacturing.ExecuteInTransactionAsync(async token =>
        {
            var order = await _manufacturing.GetWorkOrderByIdAsync(request.WorkOrderId, token)
                ?? throw new ManufacturingValidationException(
                    ManufacturingErrorCodes.WorkOrderNotFound,
                    $"Work order '{request.WorkOrderId}' was not found in this tenant.");

            if (order.CompanyId != request.CompanyId)
            {
                throw new ManufacturingValidationException(
                    ManufacturingErrorCodes.WorkOrderNotFound,
                    $"Work order '{order.OrderNumber}' does not belong to company '{request.CompanyId}'.");
            }

            WorkOrderValidator.EnsureValidProducedQuantity(request.ProducedQuantity, order.QuantityToProduce);

            if (order.Status != WorkOrderStatus.InProcess)
            {
                throw new ManufacturingValidationException(
                    ManufacturingErrorCodes.InvalidStatusTransition,
                    $"Only an InProcess work order can be completed; order '{order.OrderNumber}' is '{order.Status}'.");
            }

            var bom = await _manufacturing.GetBomByIdAsync(order.BomId, token)
                ?? throw new ManufacturingValidationException(
                    ManufacturingErrorCodes.BomNotFound,
                    $"BOM '{order.BomId}' referenced by work order '{order.OrderNumber}' was not found in this tenant.");

            if (!bom.IsActive)
            {
                throw new ManufacturingValidationException(
                    ManufacturingErrorCodes.InactiveBom,
                    $"BOM '{bom.BomNumber}' is inactive and work order '{order.OrderNumber}' cannot be completed against it.");
            }

            if (bom.Quantity <= 0)
            {
                throw new ManufacturingValidationException(
                    ManufacturingErrorCodes.InvalidBomQuantity,
                    $"BOM '{bom.BomNumber}' has no valid yield quantity for scaling the consumption lines.");
            }

            var company = await _companies.GetByIdAsync(request.CompanyId, token)
                ?? throw new StockValidationException(
                    StockErrorCodes.CompanyNotFound,
                    $"Company '{request.CompanyId}' was not found in this tenant.");

            // tasks.md 2.2 / spec AC-04: hard fiscal period lock. Checked FIRST - before a single
            // GLEntry or StockLedgerEntry line is built - so a back-dated attempt modifies ZERO data.
            company.EnsurePostingDateUnlocked(request.PostingDate);

            var wipWarehouse = await RequireCompanyWarehouseAsync(order.WipWarehouseId, company.Id, "WIP transit", token);
            var targetWarehouse = await RequireCompanyWarehouseAsync(order.TargetWarehouseId, company.Id, "target (finished goods)", token);

            var componentIds = bom.Items.Select(i => i.ItemId).Distinct().ToList();
            var itemsById = await LoadItemsAsync(componentIds, bom.ItemId, token);
            var finishedItem = itemsById[bom.ItemId];
            EnsureFifoSupported(finishedItem);

            // Planned-operations operating cost, scaled to the produced quantity. Workstation rates
            // resolve per operation through the manufacturing repository (Block A member).
            var operatingCost = await CalculateOperatingCostAsync(bom, request.ProducedQuantity, token);
            var scrapSalvage = Round4(bom.ScrapCost * request.ProducedQuantity / bom.Quantity);

            if (scrapSalvage != 0m)
            {
                throw new ManufacturingValidationException(
                    ManufacturingErrorCodes.ScrapValuationNotSupported,
                    $"BOM '{bom.BomNumber}' carries scrap salvage value {scrapSalvage:0.####}, which has no "
                    + "scrap GL account to balance against: manufacture of scrap-bearing BOMs stays deferred "
                    + "(no scrap-warehouse flow exists). Complete a scrap-free revision instead.");
            }

            // Task MF-06 (overselling prevention): BEFORE any FIFO layer is read, take the
            // UPDLOCK/HOLDLOCK range lock over the Kardex rows this posting consumes (WIP) and
            // inserts into (finished-goods target) - concurrent completions of the same WIP queue
            // HERE and re-read committed layers instead of double-consuming them.
            var lockedItemIds = componentIds.Contains(bom.ItemId)
                ? componentIds
                : componentIds.Concat(new[] { bom.ItemId }).ToList();
            await _stock.LockStockRangeAsync(lockedItemIds, new[] { wipWarehouse.Id, targetWarehouse.Id }, token);

            // Pass 1 - consume EVERY component before building a single row: an over-consumption
            // throws BEFORE any write, so the rejection provably persists zero rows.
            var consumptions = new List<(BomItem Line, Item Item, FifoResult Consumption)>(bom.Items.Count);
            foreach (var line in bom.Items.OrderBy(i => i.ItemId))
            {
                var item = itemsById[line.ItemId];
                EnsureFifoSupported(item);

                var required = Round4(line.Quantity * request.ProducedQuantity / bom.Quantity);
                if (required <= 0)
                {
                    throw new ManufacturingValidationException(
                        ManufacturingErrorCodes.InvalidBomItemQuantity,
                        $"BOM '{bom.BomNumber}' line for item '{item.ItemCode}' scales to a non-positive "
                        + $"quantity (received {required:0.####}) for produced quantity {request.ProducedQuantity:0.####}.");
                }

                var layers = await _stock.GetFifoLayersAsync(line.ItemId, wipWarehouse.Id, request.PostingDate, token);
                var consumption = FifoValuation.Consume(
                    FifoValuation.BuildLayers(layers), required, company.AllowNegativeStock,
                    item.ItemCode, wipWarehouse.WarehouseCode);

                consumptions.Add((line, item, consumption));
            }

            // Pass 2 - amounts stay EXACT (raw + operating); only the indicative unit rate comes
            // from the cost engine, mirroring the stock engine (exact amounts, indicative rates).
            var rawMaterialCost = Round4(consumptions.Sum(c => c.Consumption.TotalCost));
            var finishedTotalCost = Round4(rawMaterialCost + operatingCost);
            var unitRate = ManufacturingCostEngine.CalculateFinishedUnitCost(
                rawMaterialCost, operatingCost, scrapSalvageValue: 0m, request.ProducedQuantity);

            var wipAccount = await RequirePostableAccountAsync(
                wipWarehouse.AccountId ?? Guid.Empty, company.Id, $"warehouse '{wipWarehouse.WarehouseCode}'", token);
            var finishedAccount = await RequirePostableAccountAsync(
                targetWarehouse.AccountId ?? Guid.Empty, company.Id, $"warehouse '{targetWarehouse.WarehouseCode}'", token);
            var absorptionAccount = await RequireAccountByCodeAsync(
                company.Id, company.CogsAccountCode, "Company.CogsAccountCode", token);

            var ledger = new List<StockLedgerEntry>(consumptions.Count + 1);
            var glLines = new List<GLEntry>(6);

            foreach (var (line, item, consumption) in consumptions)
            {
                var required = Round4(line.Quantity * request.ProducedQuantity / bom.Quantity);
                ledger.Add(NewLedgerEntry(
                    line.ItemId, wipWarehouse.Id, request.PostingDate,
                    -required, consumption.AverageRate, -consumption.TotalCost));
            }

            ledger.Add(NewLedgerEntry(
                bom.ItemId, targetWarehouse.Id, request.PostingDate,
                +request.ProducedQuantity, unitRate, +finishedTotalCost));

            // Spec MF-03: Debit 1330 finished goods / Credit 1320 WIP / Credit 5210 absorption.
            AddGlLine(glLines, request, company.Id, finishedAccount, debit: finishedTotalCost, credit: 0m, finishedItem);
            AddGlLine(glLines, request, company.Id, wipAccount, debit: 0m, credit: rawMaterialCost, finishedItem);
            AddGlLine(glLines, request, company.Id, absorptionAccount, debit: 0m, credit: operatingCost, finishedItem);

            // Constitution III.1: balance must hold to four decimals BEFORE anything is saved.
            DoubleEntryGuard.EnsureBalanced(glLines);

            var stockEntry = new StockEntry
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                WarehouseId = wipWarehouse.Id,
                TargetWarehouseId = targetWarehouse.Id,
                EntryType = StockEntryType.Manufacture,
                PostingDate = request.PostingDate,
                VoucherNo = await _stock.NextVoucherNumberAsync(company.Id, VoucherPrefix, request.PostingDate.Year, token),
                CreatedAt = DateTimeOffset.UtcNow,
                Items = new List<StockEntryItem>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        ItemId = bom.ItemId,
                        Qty = request.ProducedQuantity,
                        Rate = unitRate,
                        LineNumber = 1,
                    },
                },
            };

            foreach (var entry in ledger)
            {
                entry.StockEntryId = stockEntry.Id;
                entry.VoucherType = VoucherType;
                entry.VoucherNo = stockEntry.VoucherNo;
            }

            foreach (var glLine in glLines)
            {
                glLine.VoucherNo = stockEntry.VoucherNo;
                glLine.VoucherId = stockEntry.Id;
            }

            await _stock.AddStockEntryAsync(stockEntry, token);
            await _stock.AddLedgerEntriesAsync(ledger, token);
            await _stock.AddGlEntriesAsync(glLines, token);

            order.Complete();
            order.ProducedQuantity = request.ProducedQuantity;
            order.ActualEndDate = request.PostingDate;
            await _manufacturing.UpdateWorkOrderAsync(order, token);

            return BuildResult(stockEntry, ledger, glLines, itemsById);
        }, cancellationToken);
    }

    // ----------------------------------------------------------------- validation & resolution

    private async Task<Warehouse> RequireCompanyWarehouseAsync(
        Guid warehouseId, Guid companyId, string role, CancellationToken token)
    {
        var warehouse = await _warehouses.GetByIdAsync(warehouseId, token)
            ?? throw new StockValidationException(
                StockErrorCodes.WarehouseNotFound,
                $"The {role} warehouse '{warehouseId}' was not found in this tenant.");

        if (warehouse.CompanyId != companyId)
        {
            throw new StockValidationException(
                StockErrorCodes.WarehouseNotFound,
                $"The {role} warehouse '{warehouse.WarehouseCode}' does not belong to company '{companyId}'.");
        }

        return warehouse;
    }

    private async Task<Dictionary<Guid, Item>> LoadItemsAsync(
        IReadOnlyList<Guid> componentIds, Guid finishedItemId, CancellationToken token)
    {
        var ids = componentIds.Contains(finishedItemId)
            ? componentIds.ToList()
            : componentIds.Concat(new[] { finishedItemId }).ToList();

        foreach (var id in ids)
        {
            if (id == Guid.Empty)
            {
                throw new StockValidationException(StockErrorCodes.ItemNotFound, "A BOM line references an empty ItemId.");
            }
        }

        var found = await _items.GetByIdsAsync(ids, token);
        var byId = new Dictionary<Guid, Item>(found.Count);
        foreach (var item in found)
        {
            byId[item.Id] = item;
        }

        foreach (var id in ids)
        {
            if (!byId.ContainsKey(id))
            {
                throw new StockValidationException(
                    StockErrorCodes.ItemNotFound,
                    $"Item '{id}' was not found in this tenant.");
            }
        }

        return byId;
    }

    private static void EnsureFifoSupported(Item item)
    {
        if (item.ValuationMethod != ValuationMethod.Fifo)
        {
            // Phase 3 implements FIFO ONLY (decision D5): do not half-build LIFO / Moving Average.
            throw new NotSupportedException(
                $"Item '{item.ItemCode}' uses valuation method '{item.ValuationMethod}', which is not supported yet. "
                + "Phase 3 of the perpetual inventory engine implements ValuationMethod.Fifo only.");
        }
    }

    private async Task<decimal> CalculateOperatingCostAsync(
        BillOfMaterials bom, decimal producedQuantity, CancellationToken token)
    {
        var perYield = 0m;
        foreach (var operation in bom.Operations)
        {
            var workstation = await _manufacturing.GetWorkstationByIdAsync(operation.WorkstationId, token)
                ?? throw new ManufacturingValidationException(
                    ManufacturingErrorCodes.WorkstationNotFound,
                    $"Workstation '{operation.WorkstationId}' referenced by BOM '{bom.BomNumber}' was not found in this tenant.");

            perYield += BomOperation.CalculateCost(operation.DurationMinutes, workstation.HourRateTotal);
        }

        // Planned-operations basis: the engineered cost per yield unit, scaled to production.
        return Round4(perYield * producedQuantity / bom.Quantity);
    }

    /// <summary>
    /// Constitution III.3: every GL-referenced account must exist, be active, be a leaf (posting)
    /// account and belong to the company whose books we are writing.
    /// </summary>
    private async Task<Account> RequirePostableAccountAsync(
        Guid accountId,
        Guid companyId,
        string ownerContext,
        CancellationToken token)
    {
        if (accountId == Guid.Empty)
        {
            throw new StockPostingConfigurationException(
                $"No stock account is linked to {ownerContext}; link an active leaf account before posting manufacture.");
        }

        var account = await _accounts.GetByIdAsync(accountId, token)
            ?? throw new StockPostingConfigurationException(
                $"The stock account '{accountId}' linked to {ownerContext} does not exist.");

        if (!account.IsActive)
        {
            throw new StockValidationException(
                StockErrorCodes.InvalidGlAccount,
                $"Account '{account.AccountCode}' ({ownerContext}) is inactive and cannot receive General Ledger postings.");
        }

        if (account.IsGroup)
        {
            throw new StockValidationException(
                StockErrorCodes.InvalidGlAccount,
                $"Account '{account.AccountCode}' ({ownerContext}) is a group account and cannot receive General Ledger postings.");
        }

        if (account.CompanyId != companyId)
        {
            throw new StockValidationException(
                StockErrorCodes.InvalidGlAccount,
                $"Account '{account.AccountCode}' belongs to another company and cannot be posted from this company.");
        }

        return account;
    }

    /// <summary>
    /// Decision D3: company-level GL defaults are stored as account CODES and resolved here to
    /// exactly one active leaf account of the company.
    /// </summary>
    private async Task<Account> RequireAccountByCodeAsync(
        Guid companyId,
        string? accountCode,
        string settingName,
        CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(accountCode))
        {
            throw new StockPostingConfigurationException(
                $"Company '{companyId}' does not configure {settingName}. "
                + "Seed it with an active leaf account code (e.g. UPDATE dbo.Company SET CogsAccountCode = '5210').");
        }

        var matches = await _accounts.FindActiveLeafByCodeAsync(companyId, accountCode, token);

        if (matches.Count == 0)
        {
            throw new StockPostingConfigurationException(
                $"{settingName} = '{accountCode}' does not resolve to an ACTIVE LEAF account of company "
                + $"'{companyId}'. Seed the account (IsActive = 1, IsGroup = 0) or fix the company setting.");
        }

        if (matches.Count > 1)
        {
            throw new StockPostingConfigurationException(
                $"{settingName} = '{accountCode}' is ambiguous: {matches.Count} active leaf accounts share that "
                + $"code in company '{companyId}'. Account codes must be unique per company.");
        }

        return matches[0];
    }

    // ------------------------------------------------------------------------------- persistence

    private static StockLedgerEntry NewLedgerEntry(
        Guid itemId,
        Guid warehouseId,
        DateOnly postingDate,
        decimal qtyChange,
        decimal valuationRate,
        decimal amount) =>
        new()
        {
            Id = Guid.NewGuid(),
            ItemId = itemId,
            WarehouseId = warehouseId,
            PostingDate = postingDate,
            QtyChange = qtyChange,
            ValuationRate = valuationRate,
            Amount = amount,
            CreatedAt = DateTimeOffset.UtcNow,
        };

    private static void AddGlLine(
        List<GLEntry> glLines,
        WorkOrderCompletionRequest request,
        Guid companyId,
        Account account,
        decimal debit,
        decimal credit,
        Item finishedItem) =>
        glLines.Add(new GLEntry
        {
            CompanyId = companyId,
            PostingDate = request.PostingDate,
            AccountId = account.Id,
            Account = account,
            Debit = Round4(debit),
            Credit = Round4(credit),
            DebitInAccountCurrency = Round4(debit),
            CreditInAccountCurrency = Round4(credit),
            AccountCurrency = account.Currency,
            VoucherType = VoucherType,
            VoucherNo = string.Empty,
            VoucherId = Guid.Empty,
            PartyType = null,
            PartyId = null,
            CostCenterId = null,
            IsCancelled = false,
            Remarks = $"{StockEntryType.Manufacture}: {finishedItem.ItemCode} x{request.ProducedQuantity:0.####}",
        });

    private static StockEntryPostingDto BuildResult(
        StockEntry stockEntry,
        IReadOnlyList<StockLedgerEntry> ledger,
        IReadOnlyList<GLEntry> glLines,
        IReadOnlyDictionary<Guid, Item> items)
    {
        var lines = new List<StockEntryLineDto>(stockEntry.Items.Count);
        foreach (var line in stockEntry.Items.OrderBy(l => l.LineNumber))
        {
            var item = items[line.ItemId];
            lines.Add(new StockEntryLineDto(line.ItemId, item.ItemCode, item.ItemName, line.Qty, line.Rate, line.LineNumber));
        }

        var dto = new StockEntryDto(
            stockEntry.Id,
            stockEntry.CompanyId,
            stockEntry.EntryType,
            stockEntry.PostingDate,
            stockEntry.VoucherNo,
            stockEntry.WarehouseId,
            stockEntry.TargetWarehouseId,
            stockEntry.CreatedAt,
            lines);

        var ledgerDtos = ledger
            .Select(e => new StockLedgerEntryDto(e.Id, e.ItemId, e.WarehouseId, e.PostingDate, e.QtyChange, e.ValuationRate, e.Amount))
            .ToList();

        var totalDebit = 0m;
        var totalCredit = 0m;
        var glDtos = new List<GLEntryDto>(glLines.Count);
        foreach (var line in glLines)
        {
            totalDebit += line.Debit;
            totalCredit += line.Credit;
            glDtos.Add(new GLEntryDto(
                line.Id,
                line.AccountId,
                line.Account?.AccountCode ?? string.Empty,
                line.Account?.AccountName ?? string.Empty,
                line.PostingDate,
                line.Debit,
                line.Credit,
                line.VoucherType,
                line.VoucherNo,
                line.Remarks));
        }

        return new StockEntryPostingDto(dto, ledgerDtos, glDtos, Round4(totalDebit), Round4(totalCredit));
    }

    private static decimal Round4(decimal value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);
}
