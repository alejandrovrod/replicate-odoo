using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using Erp.Domain.Services;

namespace Erp.Application.Services;

/// <summary>
/// The perpetual inventory posting engine of Task 3.2. Responsibilities, in order, all inside one
/// transaction provided by <see cref="IStockRepository.ExecuteInTransactionAsync{T}"/>:
/// validation -> FIFO valuation (pure Domain) -> StockLedgerEntry rows -> balanced General Ledger
/// lines -> gapless voucher number -> save (Constitution III.1/III.4).
/// </summary>
/// <remarks>
/// GL mapping (decision D4) reproduces the numbered spec scenarios verbatim:
/// receipt = Debit warehouse stock account / Credit Company.StockReceivedAccountCode (ST-01:
/// Dr 1310, Cr 2120); issue = Debit item expense account / Credit warehouse stock account at the
/// FIFO cost (ST-02: Dr 5210, Cr 1310); transfer preserves value and only books GL when the two
/// warehouses use different stock accounts.
/// </remarks>
public sealed class StockPostingService : IStockPostingService
{
    private const string VoucherType = "StockEntry";

    private readonly ICompanyRepository _companies;
    private readonly IAccountRepository _accounts;
    private readonly IWarehouseRepository _warehouses;
    private readonly IItemRepository _items;
    private readonly IStockRepository _stock;
    private readonly IBatchSerialService? _tracking;

    public StockPostingService(
        ICompanyRepository companies,
        IAccountRepository accounts,
        IWarehouseRepository warehouses,
        IItemRepository items,
        IStockRepository stock,
        IBatchSerialService? tracking = null)
    {
        _companies = companies;
        _accounts = accounts;
        _warehouses = warehouses;
        _items = items;
        _stock = stock;
        _tracking = tracking;
    }

    /// <summary>
    /// Batch-and-serial Fase 3: the tracking pipeline (gates + transitions). In production DI
    /// always provides it; legacy direct constructions (older unit tests) only post untracked
    /// items, so a tracked item without the pipeline fails closed instead of silently skipping.
    /// </summary>
    private IBatchSerialService TrackingOrThrow(string itemCode) =>
        _tracking ?? throw new StockValidationException(
            StockErrorCodes.BatchRequired,
            $"Item '{itemCode}' is batch/serial-tracked but no tracking service is wired.");

    public async Task<StockEntryPostingDto> PostAsync(StockPostingRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Lines is null || request.Lines.Count == 0)
        {
            throw new StockValidationException(
                StockErrorCodes.NoLines,
                "A stock entry must contain at least one line.");
        }

        // One transaction for the WHOLE posting: validation, FIFO, SLE, GL, voucher and save
        // commit together or not at all (a rollback releases the voucher lock and consumes no number).
        return await _stock.ExecuteInTransactionAsync(async token =>
        {
            var company = await _companies.GetByIdAsync(request.CompanyId, token)
                ?? throw new StockValidationException(
                    StockErrorCodes.CompanyNotFound,
                    $"Company '{request.CompanyId}' was not found in this tenant.");

            // tasks.md 2.2 / spec AC-04: hard fiscal period lock. Checked FIRST - before a single
            // GLEntry or StockLedgerEntry line is built - so a back-dated attempt against a frozen
            // company modifies ZERO data (the exception unwinds the transaction).
            company.EnsurePostingDateUnlocked(request.PostingDate);
            // R-13 FC-04: closed fiscal year rejects the posting too (second half of plan.md §3).
            await _companies.EnsurePostingDateInOpenYearAsync(company.Id, request.PostingDate, token);

            var (sourceWarehouse, targetWarehouse) = await ResolveWarehousesAsync(request, token);
            var items = await LoadItemsAsync(request, token);

            // Task 3.9 (overselling prevention): BEFORE any FIFO layer is read, take the
            // UPDLOCK/HOLDLOCK range lock over the Kardex rows this posting is about to consume
            // (source warehouse, plus target for a transfer - it inserts into that range too).
            // Concurrent consumers of the same (item, warehouse) queue HERE and re-read the
            // committed layers afterwards instead of double-consuming them: a plain SELECT race
            // lets every transaction see the same on-hand quantity and all of them win. Receipts
            // only append rows, so they never take the lock.
            if (request.EntryType != StockEntryType.MaterialReceipt)
            {
                var lockedWarehouses = targetWarehouse is null
                    ? new[] { sourceWarehouse.Id }
                    : new[] { sourceWarehouse.Id, targetWarehouse.Id };

                await _stock.LockStockRangeAsync(
                    request.Lines.Select(line => line.ItemId).Distinct().ToList(),
                    lockedWarehouses,
                    token);
            }

            // Resolve + sanity-check the GL accounts BEFORE any write (Constitution III.3).
            var sourceStockAccount = await RequirePostableAccountAsync(
                sourceWarehouse.AccountId ?? Guid.Empty, company.Id, $"warehouse '{sourceWarehouse.WarehouseCode}'", token);

            Account? targetStockAccount = null;
            if (targetWarehouse is not null)
            {
                targetStockAccount = await RequirePostableAccountAsync(
                    targetWarehouse.AccountId ?? Guid.Empty, company.Id, $"warehouse '{targetWarehouse.WarehouseCode}'", token);
            }

            var ledger = new List<StockLedgerEntry>(request.Lines.Count);
            var glLines = new List<GLEntry>(request.Lines.Count * 2);
            var consumedByLine = new Dictionary<int, FifoResult>(request.Lines.Count);

            // Batch-and-serial Fase 3 per-voucher tracking state: batch consumption inside
            // this voucher (so two lines cannot consume the same batch units twice) and the
            // staged serial transitions, persisted after the voucher number is assigned.
            var consumedByBatch = new Dictionary<Guid, decimal>();
            var serialsToAdd = new List<SerialNo>();
            var serialsToUpdate = new List<SerialNo>();
            var serialsByLine = new Dictionary<int, List<SerialNo>>();
            var batchByLine = new Dictionary<int, Guid?>();
            var batchNumbers = new Dictionary<Guid, string>();

            for (var i = 0; i < request.Lines.Count; i++)
            {
                var line = request.Lines[i];
                StockEntryValidator.EnsureValidLine(request.EntryType, line.Qty, line.Rate);
                var item = items[line.ItemId];

                var batchId = await PlanLineTrackingAsync(
                    request, company, line, item, sourceWarehouse, targetWarehouse, i,
                    consumedByBatch, serialsToAdd, serialsToUpdate, serialsByLine, batchByLine,
                    batchNumbers, token);

                switch (request.EntryType)
                {
                    case StockEntryType.MaterialReceipt:
                        await BuildReceiptLineAsync(
                            request, company, line, item, sourceWarehouse, sourceStockAccount,
                            batchId, ledger, glLines, token);
                        break;

                    case StockEntryType.MaterialIssue:
                        consumedByLine[i] = await BuildIssueLineAsync(
                            request, company, line, item, sourceWarehouse, sourceStockAccount,
                            batchId, ledger, glLines, token);
                        break;

                    case StockEntryType.MaterialTransfer:
                        consumedByLine[i] = await BuildTransferLineAsync(
                            request, company, line, item, sourceWarehouse, targetWarehouse!,
                            sourceStockAccount, targetStockAccount!, batchId, ledger, glLines, token);
                        break;

                    case StockEntryType.Manufacture:
                        throw new StockValidationException(
                            StockErrorCodes.InvalidValuationMethod,
                            "A manufacture voucher cannot be posted through the stock engine: it carries "
                            + "work-order costing (WIP consumption + operating absorption) and posts only "
                            + "through IManufacturingPostingService.");

                    default:
                        throw new StockValidationException(
                            StockErrorCodes.InvalidValuationMethod,
                            $"Unknown stock entry type '{request.EntryType}'.");
                }
            }

            // Constitution III.1: balance must hold to four decimals BEFORE anything is saved.
            DoubleEntryGuard.EnsureBalanced(glLines);

            var stockEntry = new StockEntry
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                WarehouseId = sourceWarehouse.Id,
                TargetWarehouseId = targetWarehouse?.Id,
                EntryType = request.EntryType,
                PostingDate = request.PostingDate,
                VoucherNo = await NextVoucherAsync(request, company.Id, token),

                // Set explicitly: the column default (SYSDATETIMEOFFSET) is not read back into the
                // entity, so without this the 201 response would report 0001-01-01T00:00:00.
                CreatedAt = DateTimeOffset.UtcNow,

                Items = BuildLines(request, items, consumedByLine, batchByLine),
            };

            // The Kardex rows belong to this voucher aggregate; the GL lines share its number.
            foreach (var entry in ledger)
            {
                entry.StockEntryId = stockEntry.Id;
                entry.VoucherType = VoucherType;
                entry.VoucherNo = stockEntry.VoucherNo;
            }

            foreach (var glLine in glLines)
            {
                glLine.VoucherNo = stockEntry.VoucherNo;

                // plan.md §2: VoucherId is the durable link to the source aggregate; a stock
                // entry has no counterparty dimension, so PartyType/PartyId stay null.
                glLine.VoucherId = stockEntry.Id;
            }

            await _stock.AddStockEntryAsync(stockEntry, token);
            await _stock.AddLedgerEntriesAsync(ledger, token);
            await _stock.AddGlEntriesAsync(glLines, token);

            // Batch-and-serial Fase 3: link staged serials to their persisted lines (ids only
            // exist now) and persist the transitions with everything else, atomically.
            if (serialsByLine.Count > 0 && _tracking is not null)
            {
                var persistedLines = stockEntry.Items.OrderBy(l => l.LineNumber).ToList();
                foreach (var (lineIndex, staged) in serialsByLine)
                {
                    var persistedLineId = persistedLines[lineIndex].Id;
                    foreach (var serial in staged)
                    {
                        serial.StockEntryItemId = persistedLineId;

                        // Issues stamp the voucher as the dispatch document (transfers keep
                        // moving silently; receipts use the purchase document when born there).
                        if (request.EntryType == StockEntryType.MaterialIssue)
                        {
                            serial.DeliveryDocumentNo = stockEntry.VoucherNo;
                        }
                    }
                }

                await _tracking.PersistSerialsAsync(serialsToAdd, serialsToUpdate, token);
            }

            return BuildResult(stockEntry, ledger, glLines, items, batchByLine, batchNumbers, serialsByLine);
        }, cancellationToken);
    }

    // ----------------------------------------------------------------- validation & resolution

    /// <summary>
    /// Batch-and-serial Fase 3 per-line planning: shape validation (batch named, serial
    /// count == qty), batch gates (receipt top-up-or-create, issue sufficiency/expiry with
    /// in-voucher consumption) and serial staging. Returns the resolved batch id (null for
    /// untracked items) for the Kardex rows and persisted lines. Serial doc refs and line
    /// links are stamped after numbering; persistence happens with everything else.
    /// </summary>
    private async Task<Guid?> PlanLineTrackingAsync(
        StockPostingRequest request,
        Company company,
        StockPostingLine line,
        Item item,
        Warehouse sourceWarehouse,
        Warehouse? targetWarehouse,
        int lineIndex,
        Dictionary<Guid, decimal> consumedByBatch,
        List<SerialNo> serialsToAdd,
        List<SerialNo> serialsToUpdate,
        Dictionary<int, List<SerialNo>> serialsByLine,
        Dictionary<int, Guid?> batchByLine,
        Dictionary<Guid, string> batchNumbers,
        CancellationToken token)
    {
        var isReceipt = request.EntryType == StockEntryType.MaterialReceipt;
        var cleanedSerials = StockEntryValidator.EnsureTrackingShape(
            item.ItemCode,
            item.HasBatchNo,
            line.BatchId,
            line.BatchNo,
            isReceipt,
            item.HasSerialNo,
            line.SerialNumbers,
            line.Qty);

        if (!item.HasBatchNo && !item.HasSerialNo)
        {
            batchByLine[lineIndex] = null;
            return null;
        }

        var tracking = TrackingOrThrow(item.ItemCode);
        Guid? batchId = null;

        if (item.HasBatchNo)
        {
            if (isReceipt)
            {
                var batch = await tracking.EnsureBatchForReceiptAsync(
                    item.Id, item.ItemCode, line.BatchNo!, line.ManufacturingDate, line.ExpiryDate, token);
                batchId = batch.Id;
                batchNumbers[batch.Id] = batch.BatchId;
            }
            else
            {
                var batch = await tracking.EnsureBatchForIssueAsync(
                    item.Id, item.ItemCode, line.BatchId!.Value, sourceWarehouse.Id,
                    sourceWarehouse.WarehouseCode, line.Qty, request.PostingDate,
                    company.AllowNegativeStock, consumedByBatch, token);
                batchId = batch.Id;
                batchNumbers[batch.Id] = batch.BatchId;
                consumedByBatch[batchId.Value] = consumedByBatch.TryGetValue(batchId.Value, out var used)
                    ? used + line.Qty
                    : line.Qty;
            }
        }

        batchByLine[lineIndex] = batchId;

        if (item.HasSerialNo)
        {
            if (isReceipt)
            {
                var staged = await tracking.StageReceiptSerialsAsync(
                    item.Id, item.ItemCode, cleanedSerials, sourceWarehouse.Id, batchId,
                    request.PostingDate, token);
                serialsToAdd.AddRange(staged);
                serialsByLine[lineIndex] = staged.ToList();
            }
            else if (request.EntryType == StockEntryType.MaterialTransfer)
            {
                var staged = await tracking.StageTransferSerialsAsync(
                    item.Id, item.ItemCode, cleanedSerials, sourceWarehouse.Id,
                    sourceWarehouse.WarehouseCode, targetWarehouse!.Id, token);
                serialsToUpdate.AddRange(staged);
                serialsByLine[lineIndex] = staged.ToList();
            }
            else
            {
                var staged = await tracking.StageIssueSerialsAsync(
                    item.Id, item.ItemCode, cleanedSerials, sourceWarehouse.Id,
                    sourceWarehouse.WarehouseCode, request.PostingDate, token);
                serialsToUpdate.AddRange(staged);
                serialsByLine[lineIndex] = staged.ToList();
            }
        }

        return batchId;
    }

    private async Task<(Warehouse Source, Warehouse? Target)> ResolveWarehousesAsync(
        StockPostingRequest request,
        CancellationToken token)
    {
        var source = await _warehouses.GetByIdAsync(request.WarehouseId, token)
            ?? throw new StockValidationException(
                StockErrorCodes.WarehouseNotFound,
                $"Warehouse '{request.WarehouseId}' was not found in this tenant.");

        if (source.CompanyId != request.CompanyId)
        {
            throw new StockValidationException(
                StockErrorCodes.WarehouseNotFound,
                $"Warehouse '{source.WarehouseCode}' does not belong to company '{request.CompanyId}'.");
        }

        if (request.EntryType != StockEntryType.MaterialTransfer)
        {
            if (request.TargetWarehouseId is not null)
            {
                throw new StockValidationException(
                    StockErrorCodes.InvalidTargetWarehouse,
                    "Only a material transfer may declare a target warehouse.");
            }

            return (source, null);
        }

        if (request.TargetWarehouseId is not { } targetId)
        {
            throw new StockValidationException(
                StockErrorCodes.InvalidTargetWarehouse,
                "A material transfer requires a target warehouse (TargetWarehouseId).");
        }

        if (targetId == source.Id)
        {
            throw new StockValidationException(
                StockErrorCodes.InvalidTargetWarehouse,
                "The target warehouse must differ from the source warehouse.");
        }

        var target = await _warehouses.GetByIdAsync(targetId, token)
            ?? throw new StockValidationException(
                StockErrorCodes.WarehouseNotFound,
                $"Target warehouse '{targetId}' was not found in this tenant.");

        if (target.CompanyId != request.CompanyId)
        {
            throw new StockValidationException(
                StockErrorCodes.InvalidTargetWarehouse,
                $"Target warehouse '{target.WarehouseCode}' must belong to the same company as the source warehouse.");
        }

        return (source, target);
    }

    private async Task<Dictionary<Guid, Item>> LoadItemsAsync(StockPostingRequest request, CancellationToken token)
    {
        var ids = new List<Guid>(request.Lines.Count);
        foreach (var line in request.Lines)
        {
            if (line.ItemId == Guid.Empty)
            {
                throw new StockValidationException(StockErrorCodes.ItemNotFound, "A line references an empty ItemId.");
            }

            if (!ids.Contains(line.ItemId))
            {
                ids.Add(line.ItemId);
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
                $"No stock account is linked to {ownerContext}; link an active leaf account before posting stock.");
        }

        var account = await _accounts.GetByIdAsync(accountId, token)
            ?? throw new StockPostingConfigurationException(
                $"The stock account '{accountId}' linked to {ownerContext} does not exist.");

        EnsurePostable(account, companyId, ownerContext);
        return account;
    }

    private static void EnsurePostable(Account account, Guid companyId, string ownerContext)
    {
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
    }

    /// <summary>
    /// Decision D3: company-level GL defaults are stored as account CODES (a Company -&gt; Account FK
    /// would be circular, because Account already references Company) and resolved here to exactly
    /// one active leaf account of the company.
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
                + "Seed it with an active leaf account code (e.g. UPDATE dbo.Company SET StockReceivedAccountCode = '2120').");
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

    // --------------------------------------------------------------------------- line builders

    private async Task BuildReceiptLineAsync(
        StockPostingRequest request,
        Company company,
        StockPostingLine line,
        Item item,
        Warehouse warehouse,
        Account stockAccount,
        Guid? batchId,
        List<StockLedgerEntry> ledger,
        List<GLEntry> glLines,
        CancellationToken token)
    {
        var rate = line.Rate!.Value;
        var amount = Round4(line.Qty * rate);

        var receivedAccount = await RequireAccountByCodeAsync(
            company.Id,
            company.StockReceivedAccountCode,
            "Company.StockReceivedAccountCode",
            token);

        ledger.Add(NewLedgerEntry(line, warehouse.Id, request, +line.Qty, rate, +amount, batchId));

        // spec ST-01: Debit 1310 Stock In Hand / Credit 2120 Stock Received But Not Billed.
        AddGlLine(glLines, request, company.Id, stockAccount, debit: amount, credit: 0m, line, item);
        AddGlLine(glLines, request, company.Id, receivedAccount, debit: 0m, credit: amount, line, item);
    }

    private async Task<FifoResult> BuildIssueLineAsync(
        StockPostingRequest request,
        Company company,
        StockPostingLine line,
        Item item,
        Warehouse warehouse,
        Account stockAccount,
        Guid? batchId,
        List<StockLedgerEntry> ledger,
        List<GLEntry> glLines,
        CancellationToken token)
    {
        EnsureFifoSupported(item);

        var cogsAccount = await RequireAccountByCodeAsync(
            company.Id,
            company.CogsAccountCode,
            "Company.CogsAccountCode",
            token);
        var layers = await GetLayersAsync(line, warehouse, request, ledger, token);
        var consumption = FifoValuation.Consume(
            layers, line.Qty, company.AllowNegativeStock, item.ItemCode, warehouse.WarehouseCode);

        ledger.Add(NewLedgerEntry(line, warehouse.Id, request, -line.Qty, consumption.AverageRate, -consumption.TotalCost, batchId));

        // spec ST-02: Debit Cost of Goods Sold / Credit Stock In Hand for the FIFO cost.
        AddGlLine(glLines, request, company.Id, cogsAccount, debit: consumption.TotalCost, credit: 0m, line, item);
        AddGlLine(glLines, request, company.Id, stockAccount, debit: 0m, credit: consumption.TotalCost, line, item);

        return consumption;
    }

    private async Task<FifoResult> BuildTransferLineAsync(
        StockPostingRequest request,
        Company company,
        StockPostingLine line,
        Item item,
        Warehouse source,
        Warehouse target,
        Account sourceStockAccount,
        Account targetStockAccount,
        Guid? batchId,
        List<StockLedgerEntry> ledger,
        List<GLEntry> glLines,
        CancellationToken token)
    {
        EnsureFifoSupported(item);

        var layers = await GetLayersAsync(line, source, request, ledger, token);
        var consumption = FifoValuation.Consume(
            layers, line.Qty, company.AllowNegativeStock, item.ItemCode, source.WarehouseCode);

        // Out of the source warehouse and INTO the target at the consumed value: value is preserved.
        ledger.Add(NewLedgerEntry(line, source.Id, request, -line.Qty, consumption.AverageRate, -consumption.TotalCost, batchId));
        ledger.Add(NewLedgerEntry(line, target.Id, request, +line.Qty, consumption.AverageRate, +consumption.TotalCost, batchId));

        if (sourceStockAccount.Id != targetStockAccount.Id)
        {
            // Different stock accounts = the asset moves between GL accounts; same account = skip GL.
            AddGlLine(glLines, request, company.Id, targetStockAccount, debit: consumption.TotalCost, credit: 0m, line, item);
            AddGlLine(glLines, request, company.Id, sourceStockAccount, debit: 0m, credit: consumption.TotalCost, line, item);
        }

        return consumption;
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

    /// <summary>
    /// Open FIFO layers for one (item, warehouse) pair up to the posting date, INCLUDING the
    /// movements this same voucher already produced - so two lines of the same item inside one
    /// voucher cannot both consume the same layer. Rows are ordered by (PostingDate, CreatedAt)
    /// inside <see cref="FifoValuation.BuildLayers"/>; this voucher's rows carry CreatedAt = now,
    /// so they sort after the already-persisted rows of the same posting date.
    /// </summary>
    private async Task<IReadOnlyList<FifoLayer>> GetLayersAsync(
        StockPostingLine line,
        Warehouse warehouse,
        StockPostingRequest request,
        List<StockLedgerEntry> voucherLedger,
        CancellationToken token)
    {
        var persisted = await _stock.GetFifoLayersAsync(line.ItemId, warehouse.Id, request.PostingDate, token);

        var siblings = new List<StockLedgerEntry>();
        foreach (var entry in voucherLedger)
        {
            if (entry.ItemId == line.ItemId && entry.WarehouseId == warehouse.Id)
            {
                siblings.Add(entry);
            }
        }

        return siblings.Count == 0
            ? FifoValuation.BuildLayers(persisted)
            : FifoValuation.BuildLayers(persisted.Concat(siblings));
    }

    // ------------------------------------------------------------------------------- persistence

    private static StockLedgerEntry NewLedgerEntry(
        StockPostingLine line,
        Guid warehouseId,
        StockPostingRequest request,
        decimal qtyChange,
        decimal valuationRate,
        decimal amount,
        Guid? batchId) =>
        new()
        {
            Id = Guid.NewGuid(),
            ItemId = line.ItemId,
            WarehouseId = warehouseId,
            BatchId = batchId,
            PostingDate = request.PostingDate,
            QtyChange = qtyChange,
            ValuationRate = valuationRate,
            Amount = amount,

            // Stamped here (instead of the SYSDATETIMEOFFSET() column default) so rows produced by
            // this voucher sort AFTER the already-persisted rows of the same posting date.
            CreatedAt = DateTimeOffset.UtcNow,

            // StockEntryId is fixed up when the header is built (one aggregate).
        };

    private static void AddGlLine(
        List<GLEntry> glLines,
        StockPostingRequest request,
        Guid companyId,
        Account account,
        decimal debit,
        decimal credit,
        StockPostingLine line,
        Item item) =>
        glLines.Add(new GLEntry
        {
            CompanyId = companyId,
            PostingDate = request.PostingDate,
            AccountId = account.Id,

            // Navigation kept populated so the API response can show account code + name without
            // a second round trip (EF fixes up the FK from the reference anyway).
            Account = account,
            Debit = Round4(debit),
            Credit = Round4(credit),

            // plan.md §2 account-currency pair: single-currency postings book the ledger amount
            // 1:1 and snapshot the account currency (multi-currency restatement = spec AC-05, later).
            DebitInAccountCurrency = Round4(debit),
            CreditInAccountCurrency = Round4(credit),
            AccountCurrency = account.Currency?.Code ?? "USD",

            VoucherType = VoucherType,

            // VoucherNo + VoucherId are stamped after the gapless number is assigned (same
            // transaction); no party dimension on stock vouchers; no cost center module yet.
            VoucherNo = string.Empty,
            VoucherId = Guid.Empty,
            PartyType = null,
            PartyId = null,
            CostCenterId = null,
            IsCancelled = false,
            Remarks = $"{request.EntryType}: {item.ItemCode} x{line.Qty:0.####}",
        });

    private async Task<string> NextVoucherAsync(StockPostingRequest request, Guid companyId, CancellationToken token)
    {
        var prefix = request.EntryType switch
        {
            StockEntryType.MaterialReceipt => "MR",
            StockEntryType.MaterialIssue => "MI",
            StockEntryType.MaterialTransfer => "MT",
            StockEntryType.Manufacture => throw new StockValidationException(
                StockErrorCodes.InvalidValuationMethod,
                "A manufacture voucher takes its MF-YYYY-NNNNN number from IManufacturingPostingService, "
                + "not from the stock voucher sequence."),
            _ => throw new StockValidationException(
                StockErrorCodes.InvalidValuationMethod,
                $"Unknown stock entry type '{request.EntryType}'."),
        };

        return await _stock.NextVoucherNumberAsync(companyId, prefix, request.PostingDate.Year, token);
    }

    private static List<StockEntryItem> BuildLines(
        StockPostingRequest request,
        IReadOnlyDictionary<Guid, Item> items,
        IReadOnlyDictionary<int, FifoResult> consumedByLine,
        IReadOnlyDictionary<int, Guid?> batchByLine)
    {
        var lines = new List<StockEntryItem>(request.Lines.Count);
        for (var i = 0; i < request.Lines.Count; i++)
        {
            var line = request.Lines[i];
            _ = items[line.ItemId];

            lines.Add(new StockEntryItem
            {
                Id = Guid.NewGuid(),
                ItemId = line.ItemId,
                Qty = line.Qty,
                LineNumber = i + 1,
                BatchId = batchByLine.TryGetValue(i, out var batchId) ? batchId : null,

                // Receipts carry the user rate; issues/transfers carry the FIFO-computed average.
                Rate = request.EntryType == StockEntryType.MaterialReceipt
                    ? line.Rate
                    : consumedByLine.TryGetValue(i, out var consumption) ? consumption.AverageRate : line.Rate,
            });
        }

        return lines;
    }

    private static StockEntryPostingDto BuildResult(
        StockEntry stockEntry,
        IReadOnlyList<StockLedgerEntry> ledger,
        IReadOnlyList<GLEntry> glLines,
        IReadOnlyDictionary<Guid, Item> items,
        IReadOnlyDictionary<int, Guid?> batchByLine,
        IReadOnlyDictionary<Guid, string> batchNumbers,
        IReadOnlyDictionary<int, List<SerialNo>> serialsByLine)
    {
        var lines = new List<StockEntryLineDto>(stockEntry.Items.Count);
        var orderedItems = stockEntry.Items.OrderBy(l => l.LineNumber).ToList();
        for (var i = 0; i < orderedItems.Count; i++)
        {
            var line = orderedItems[i];
            var item = items[line.ItemId];
            batchByLine.TryGetValue(i, out var batchId);
            serialsByLine.TryGetValue(i, out var serials);
            lines.Add(new StockEntryLineDto(
                line.ItemId,
                item.ItemCode,
                item.ItemName,
                line.Qty,
                line.Rate,
                line.LineNumber,
                batchId,
                batchId.HasValue && batchNumbers.TryGetValue(batchId.Value, out var number) ? number : null,
                serials?.Select(s => s.SerialNumber).ToList()));
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
            .Select(e => new StockLedgerEntryDto(e.Id, e.ItemId, e.WarehouseId, e.PostingDate, e.QtyChange, e.ValuationRate, e.Amount, e.BatchId))
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


