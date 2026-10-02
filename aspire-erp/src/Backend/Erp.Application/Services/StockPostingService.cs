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

    public StockPostingService(
        ICompanyRepository companies,
        IAccountRepository accounts,
        IWarehouseRepository warehouses,
        IItemRepository items,
        IStockRepository stock)
    {
        _companies = companies;
        _accounts = accounts;
        _warehouses = warehouses;
        _items = items;
        _stock = stock;
    }

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

            var (sourceWarehouse, targetWarehouse) = await ResolveWarehousesAsync(request, token);
            var items = await LoadItemsAsync(request, token);

            // Resolve + sanity-check the GL accounts BEFORE any write (Constitution III.3).
            var sourceStockAccount = await RequirePostableAccountAsync(
                sourceWarehouse.StockAccountId, company.Id, $"warehouse '{sourceWarehouse.Code}'", token);

            Account? targetStockAccount = null;
            if (targetWarehouse is not null)
            {
                targetStockAccount = await RequirePostableAccountAsync(
                    targetWarehouse.StockAccountId, company.Id, $"warehouse '{targetWarehouse.Code}'", token);
            }

            var ledger = new List<StockLedgerEntry>(request.Lines.Count);
            var glLines = new List<GLEntry>(request.Lines.Count * 2);
            var consumedByLine = new Dictionary<int, FifoResult>(request.Lines.Count);

            for (var i = 0; i < request.Lines.Count; i++)
            {
                var line = request.Lines[i];
                StockEntryValidator.EnsureValidLine(request.EntryType, line.Qty, line.Rate);
                var item = items[line.ItemId];

                switch (request.EntryType)
                {
                    case StockEntryType.MaterialReceipt:
                        await BuildReceiptLineAsync(
                            request, company, line, item, sourceWarehouse, sourceStockAccount,
                            ledger, glLines, token);
                        break;

                    case StockEntryType.MaterialIssue:
                        consumedByLine[i] = await BuildIssueLineAsync(
                            request, company, line, item, sourceWarehouse, sourceStockAccount,
                            ledger, glLines, token);
                        break;

                    case StockEntryType.MaterialTransfer:
                        consumedByLine[i] = await BuildTransferLineAsync(
                            request, company, line, item, sourceWarehouse, targetWarehouse!,
                            sourceStockAccount, targetStockAccount!, ledger, glLines, token);
                        break;

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

                Items = BuildLines(request, items, consumedByLine),
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
            }

            await _stock.AddStockEntryAsync(stockEntry, token);
            await _stock.AddLedgerEntriesAsync(ledger, token);
            await _stock.AddGlEntriesAsync(glLines, token);

            return BuildResult(stockEntry, ledger, glLines, items);
        }, cancellationToken);
    }

    // ----------------------------------------------------------------- validation & resolution

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
                $"Warehouse '{source.Code}' does not belong to company '{request.CompanyId}'.");
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
                $"Target warehouse '{target.Code}' must belong to the same company as the source warehouse.");
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

        ledger.Add(NewLedgerEntry(line, warehouse.Id, request, +line.Qty, rate, +amount));

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
        List<StockLedgerEntry> ledger,
        List<GLEntry> glLines,
        CancellationToken token)
    {
        EnsureFifoSupported(item);

        var expenseAccount = await RequireItemExpenseAccountAsync(item, token);
        var layers = await GetLayersAsync(line, warehouse, request, ledger, token);
        var consumption = FifoValuation.Consume(
            layers, line.Qty, company.AllowNegativeStock, item.Code, warehouse.Code);

        ledger.Add(NewLedgerEntry(line, warehouse.Id, request, -line.Qty, consumption.AverageRate, -consumption.TotalCost));

        // spec ST-02: Debit Cost of Goods Sold / Credit Stock In Hand for the FIFO cost.
        AddGlLine(glLines, request, company.Id, expenseAccount, debit: consumption.TotalCost, credit: 0m, line, item);
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
        List<StockLedgerEntry> ledger,
        List<GLEntry> glLines,
        CancellationToken token)
    {
        EnsureFifoSupported(item);

        var layers = await GetLayersAsync(line, source, request, ledger, token);
        var consumption = FifoValuation.Consume(
            layers, line.Qty, company.AllowNegativeStock, item.Code, source.Code);

        // Out of the source warehouse and INTO the target at the consumed value: value is preserved.
        ledger.Add(NewLedgerEntry(line, source.Id, request, -line.Qty, consumption.AverageRate, -consumption.TotalCost));
        ledger.Add(NewLedgerEntry(line, target.Id, request, +line.Qty, consumption.AverageRate, +consumption.TotalCost));

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
                $"Item '{item.Code}' uses valuation method '{item.ValuationMethod}', which is not supported yet. "
                + "Phase 3 of the perpetual inventory engine implements ValuationMethod.Fifo only.");
        }
    }

    private async Task<Account> RequireItemExpenseAccountAsync(Item item, CancellationToken token)
    {
        if (item.ExpenseAccountId is not { } accountId || accountId == Guid.Empty)
        {
            throw new StockPostingConfigurationException(
                $"Item '{item.Code}' has no ExpenseAccount configured; issues need an expense account "
                + "(e.g. 5210 Cost of Goods Sold) to debit the COGS.");
        }

        var account = await _accounts.GetByIdAsync(accountId, token)
            ?? throw new StockPostingConfigurationException(
                $"The ExpenseAccount '{accountId}' of item '{item.Code}' does not exist.");

        if (!account.IsActive || account.IsGroup)
        {
            throw new StockPostingConfigurationException(
                $"The ExpenseAccount '{account.AccountCode}' of item '{item.Code}' must be an active leaf account.");
        }

        return account;
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
        decimal amount) =>
        new()
        {
            Id = Guid.NewGuid(),
            ItemId = line.ItemId,
            WarehouseId = warehouseId,
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
            VoucherType = VoucherType,

            // VoucherNo is stamped after the gapless number is assigned (same transaction).
            VoucherNo = string.Empty,
            Remarks = $"{request.EntryType}: {item.Code} x{line.Qty:0.####}",
        });

    private async Task<string> NextVoucherAsync(StockPostingRequest request, Guid companyId, CancellationToken token)
    {
        var prefix = request.EntryType switch
        {
            StockEntryType.MaterialReceipt => "MR",
            StockEntryType.MaterialIssue => "MI",
            StockEntryType.MaterialTransfer => "MT",
            _ => throw new StockValidationException(
                StockErrorCodes.InvalidValuationMethod,
                $"Unknown stock entry type '{request.EntryType}'."),
        };

        return await _stock.NextVoucherNumberAsync(companyId, prefix, request.PostingDate.Year, token);
    }

    private static List<StockEntryItem> BuildLines(
        StockPostingRequest request,
        IReadOnlyDictionary<Guid, Item> items,
        IReadOnlyDictionary<int, FifoResult> consumedByLine)
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
        IReadOnlyDictionary<Guid, Item> items)
    {
        var lines = new List<StockEntryLineDto>(stockEntry.Items.Count);
        foreach (var line in stockEntry.Items.OrderBy(l => l.LineNumber))
        {
            var item = items[line.ItemId];
            lines.Add(new StockEntryLineDto(line.ItemId, item.Code, item.Name, line.Qty, line.Rate, line.LineNumber));
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
