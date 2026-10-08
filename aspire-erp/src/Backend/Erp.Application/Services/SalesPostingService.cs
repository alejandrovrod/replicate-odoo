using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using Erp.Domain.Services;

namespace Erp.Application.Services;

/// <summary>
/// The selling posting engine of Task 5.2b (Amendment A1). Responsibilities, in order, all inside
/// one transaction provided by <see cref="IDeliveryNoteRepository.ExecuteInTransactionAsync{T}"/>:
/// validation -&gt; SL-04 non-overdelivery guard -&gt; FIFO valuation -&gt; StockLedgerEntry rows
/// -&gt; balanced General Ledger lines -&gt; gapless DN voucher -&gt; save -&gt; sales order update
/// (Constitution III.1/III.4).
/// </summary>
/// <remarks>
/// GL mapping (tasks.md 5.2b): Debit Company.CogsAccountCode / Credit the warehouse stock account
/// at the FIFO cost of the shipment (spec ST-02's issue mapping, restated as a sale) - the same
/// Dr 5210 / Cr 1310 pair the material issue books. The delivery note itself carries NO money
/// columns (plan.md §1.6): value comes from the cost layers, never from the order rate.
/// <para>
/// Company GL defaults are CODES resolved to exactly one active leaf account (decision D3), and
/// the fiscal lock (<c>company.EnsurePostingDateUnlocked</c>) runs FIRST, before a single row is
/// built - a back-dated attempt against a frozen company modifies zero data.
/// </para>
/// </remarks>
public sealed class SalesPostingService : ISalesPostingService
{
    private const string DeliveryVoucherType = "DeliveryNote";
    private const string DeliveryVoucherPrefix = "DN";

    /// <summary>plan.md §2 GLEntry.PartyType vocabulary value for customer parties.</summary>
    private const string CustomerPartyType = "Customer";

    private readonly ICompanyRepository _companies;
    private readonly IAccountRepository _accounts;
    private readonly IWarehouseRepository _warehouses;
    private readonly IItemRepository _items;
    private readonly IStockRepository _stock;
    private readonly ISalesOrderRepository _salesOrders;
    private readonly IDeliveryNoteRepository _deliveryNotes;
    private readonly ICustomerRepository _customers;

    public SalesPostingService(
        ICompanyRepository companies,
        IAccountRepository accounts,
        IWarehouseRepository warehouses,
        IItemRepository items,
        IStockRepository stock,
        ISalesOrderRepository salesOrders,
        IDeliveryNoteRepository deliveryNotes,
        ICustomerRepository customers)
    {
        _companies = companies;
        _accounts = accounts;
        _warehouses = warehouses;
        _items = items;
        _stock = stock;
        _salesOrders = salesOrders;
        _deliveryNotes = deliveryNotes;
        _customers = customers;
    }

    public async Task<DeliveryNotePostingDto> PostDeliveryNoteAsync(
        DeliveryNotePostingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        SalesValidator.EnsureHasLines(request.Lines);

        // One transaction for the WHOLE posting: guard, FIFO, SLE, GL, voucher, save and the
        // sales-order update commit together or not at all (a rollback consumes no number).
        return await _deliveryNotes.ExecuteInTransactionAsync(async token =>
        {
            var company = await _companies.GetByIdAsync(request.CompanyId, token)
                ?? throw new StockValidationException(
                    StockErrorCodes.CompanyNotFound,
                    $"Company '{request.CompanyId}' was not found in this tenant.");

            // tasks.md 2.2 / spec AC-04: hard fiscal period lock - FIRST check, before any
            // GLEntry/StockLedgerEntry line is built, so a back-dated delivery modifies zero data.
            company.EnsurePostingDateUnlocked(request.PostingDate);
            // R-13 FC-04: closed fiscal year rejects the posting too (second half of plan.md §3).
            await _companies.EnsurePostingDateInOpenYearAsync(company.Id, request.PostingDate, token);

            var order = await _salesOrders.GetOrderByIdAsync(request.SalesOrderId, token)
                ?? throw new SalesValidationException(
                    SellingErrorCodes.SalesOrderNotFound,
                    $"Sales order '{request.SalesOrderId}' was not found in this tenant.");

            if (order.CompanyId != company.Id)
            {
                throw new SalesValidationException(
                    SellingErrorCodes.SalesOrderNotFound,
                    $"Sales order '{order.OrderNumber}' does not belong to company '{company.Id}'.");
            }

            // Task 5.2b: only a confirmed order may ship (Draft has not passed the credit gate,
            // Completed/Cancelled have nothing left to ship).
            SalesValidator.EnsureDeliverable(order.Status);

            var warehouse = await ResolveWarehouseAsync(request.WarehouseId, company.Id, token);
            var items = await LoadItemsAsync(request.Lines.Select(l => l.ItemId), token);

            // Resolve + sanity-check the GL accounts BEFORE any write (Constitution III.3).
            var stockAccount = await RequirePostableAccountAsync(
                warehouse.AccountId ?? Guid.Empty, company.Id, $"warehouse '{warehouse.WarehouseCode}'", token);
            var cogsAccount = await RequireAccountByCodeAsync(
                company.Id,
                company.CogsAccountCode,
                "Company.CogsAccountCode",
                token);

            var orderLinesById = order.Lines.ToDictionary(l => l.Id);

            // --- phase 1: EVERY SL-04 check runs before ANY FIFO layer is consumed, so a rejected
            // attempt writes zero rows (overdelivery, foreign line, wrong item, non-FIFO valuation).
            var requestedByOrderLine = new Dictionary<Guid, decimal>(request.Lines.Count);
            foreach (var line in request.Lines)
            {
                SalesValidator.EnsureValidDeliveryLine(line.Qty);

                if (!orderLinesById.TryGetValue(line.SalesOrderItemId, out var orderLine))
                {
                    throw new SalesValidationException(
                        SellingErrorCodes.SalesOrderLineMismatch,
                        $"Delivery line references order line '{line.SalesOrderItemId}', "
                        + $"which does not belong to sales order '{order.OrderNumber}'.");
                }

                if (line.ItemId != orderLine.ItemId)
                {
                    throw new SalesValidationException(
                        SellingErrorCodes.SalesOrderLineMismatch,
                        $"Delivery line item '{line.ItemId}' does not match order line item '{orderLine.ItemId}'.");
                }

                EnsureFifoSupported(items[line.ItemId]);

                // The same order line may legitimately be shipped twice inside one note (partial
                // quantities), so the guard evaluates the AGGREGATED request.
                requestedByOrderLine[orderLine.Id] =
                    requestedByOrderLine.GetValueOrDefault(orderLine.Id) + line.Qty;
            }

            foreach (var (orderLineId, requested) in requestedByOrderLine)
            {
                var orderLine = orderLinesById[orderLineId];
                var remaining = orderLine.Quantity - orderLine.DeliveredQuantity;

                if (requested > remaining)
                {
                    // spec SL-04: Quantity - DeliveredQuantity is the hard ceiling.
                    throw new OverdeliveryNotAllowedException(
                        items[orderLine.ItemId].ItemCode,
                        order.OrderNumber,
                        remaining < 0 ? 0m : remaining,
                        requested);
                }
            }

            // --- phase 2: value every line with FIFO, then build the balanced ledger pair.
            var ledger = new List<StockLedgerEntry>(request.Lines.Count);
            var glLines = new List<GLEntry>(request.Lines.Count * 2);

            foreach (var line in request.Lines)
            {
                var item = items[line.ItemId];
                var layers = await GetLayersAsync(line, warehouse, request, ledger, token);
                var consumption = FifoValuation.Consume(
                    layers, line.Qty, company.AllowNegativeStock, item.ItemCode, warehouse.WarehouseCode);

                ledger.Add(NewLedgerEntry(
                    line, warehouse.Id, request, -line.Qty, consumption.AverageRate, -consumption.TotalCost));

                // tasks.md 5.2b: Debit Cost of Goods Sold / Credit the warehouse stock account.
                AddGlLine(
                    glLines, request, company.Id, cogsAccount,
                    debit: consumption.TotalCost, credit: 0m, line, item);
                AddGlLine(
                    glLines, request, company.Id, stockAccount,
                    debit: 0m, credit: consumption.TotalCost, line, item);
            }

            // Constitution III.1: balance must hold to four decimals BEFORE anything is saved.
            DoubleEntryGuard.EnsureBalanced(glLines);

            var deliveryNote = new DeliveryNote
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                SalesOrderId = order.Id,
                WarehouseId = warehouse.Id,
                PostingDate = request.PostingDate,
                VoucherNo = await _deliveryNotes.NextDeliveryVoucherNumberAsync(
                    company.Id, request.PostingDate.Year, token),

                // Set explicitly: the column default (SYSDATETIMEOFFSET) is not read back into the
                // entity, so without this the 201 response would report 0001-01-01T00:00:00.
                CreatedAt = DateTimeOffset.UtcNow,
                Lines = BuildLines(request, items),
            };

            // The Kardex rows and the GL lines share the voucher identity of this posting.
            foreach (var entry in ledger)
            {
                entry.VoucherType = DeliveryVoucherType;
                entry.VoucherNo = deliveryNote.VoucherNo;
            }

            // plan.md §2 voucher provenance: the delivery note aggregate is the source document
            // and the customer counterparty rides on the GL lines (spec ST-02 rows have no party;
            // a sale always has one).
            foreach (var glLine in glLines)
            {
                glLine.VoucherNo = deliveryNote.VoucherNo;
                glLine.VoucherId = deliveryNote.Id;
                glLine.PartyType = CustomerPartyType;
                glLine.PartyId = order.CustomerId;
            }

            await _deliveryNotes.AddDeliveryNoteAsync(deliveryNote, token);
            await _stock.AddLedgerEntriesAsync(ledger, token);
            await _stock.AddGlEntriesAsync(glLines, token);

            // Task 5.2b: the order advances in the SAME transaction - counters, percentage and
            // workflow status. BilledPercentage belongs to Task 5.3 and is never touched here.
            ApplyDelivery(order, requestedByOrderLine);
            await _salesOrders.UpdateOrderAsync(order, token);

            var customer = order.Customer
                ?? await _customers.GetByIdAsync(order.CustomerId, token)
                ?? throw new SalesValidationException(
                    SellingErrorCodes.CustomerNotFound,
                    $"Customer '{order.CustomerId}' was not found in this tenant.");

            return BuildResult(deliveryNote, ledger, glLines, order, customer, items);
        }, cancellationToken);
    }

    // ----------------------------------------------------------------- validation & resolution

    private async Task<Warehouse> ResolveWarehouseAsync(Guid warehouseId, Guid companyId, CancellationToken token)
    {
        var warehouse = await _warehouses.GetByIdAsync(warehouseId, token)
            ?? throw new StockValidationException(
                StockErrorCodes.WarehouseNotFound,
                $"Warehouse '{warehouseId}' was not found in this tenant.");

        if (warehouse.CompanyId != companyId)
        {
            throw new StockValidationException(
                StockErrorCodes.WarehouseNotFound,
                $"Warehouse '{warehouse.WarehouseCode}' does not belong to company '{companyId}'.");
        }

        return warehouse;
    }

    private async Task<Dictionary<Guid, Item>> LoadItemsAsync(IEnumerable<Guid> itemIds, CancellationToken token)
    {
        var ids = new List<Guid>();
        foreach (var id in itemIds)
        {
            if (id == Guid.Empty)
            {
                throw new StockValidationException(StockErrorCodes.ItemNotFound, "A line references an empty ItemId.");
            }

            if (!ids.Contains(id))
            {
                ids.Add(id);
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
            // Same decision as StockPostingService (D5): do not half-build LIFO / Moving Average.
            throw new NotSupportedException(
                $"Item '{item.ItemCode}' uses valuation method '{item.ValuationMethod}', which is not supported yet. "
                + "Phase 3 of the perpetual inventory engine implements ValuationMethod.Fifo only.");
        }
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
    /// Decision D3 applied to the selling defaults: stored as account CODES (a Company -&gt; Account
    /// FK would be circular) and resolved here to exactly one active leaf account of the company.
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
                + "Seed it with an active leaf account code before posting stock.");
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

    /// <summary>
    /// Open FIFO layers for one (item, warehouse) pair up to the posting date, INCLUDING the
    /// movements this same voucher already produced - so two lines of the same item inside one
    /// voucher cannot both consume the same layer (same rule as StockPostingService).
    /// </summary>
    private async Task<IReadOnlyList<FifoLayer>> GetLayersAsync(
        DeliveryNotePostingLine line,
        Warehouse warehouse,
        DeliveryNotePostingRequest request,
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

    /// <summary>
    /// Writes the shipment back onto the order: DeliveredQuantity per line, the quantity-weighted
    /// <c>DeliveredPercentage</c> (2 decimals) and the workflow status - Completed when every line
    /// is full, PartiallyDelivered otherwise. <c>BilledPercentage</c> is Task 5.3's and stays put.
    /// </summary>
    private static void ApplyDelivery(SalesOrder order, IReadOnlyDictionary<Guid, decimal> requestedByOrderLine)
    {
        foreach (var (orderLineId, requested) in requestedByOrderLine)
        {
            var orderLine = order.Lines.First(l => l.Id == orderLineId);
            orderLine.DeliveredQuantity += requested;
        }

        var totalQuantity = order.Lines.Sum(l => l.Quantity);
        var totalDelivered = order.Lines.Sum(l => l.DeliveredQuantity);

        order.DeliveredPercentage = totalQuantity <= 0
            ? 0m
            : Math.Round(totalDelivered / totalQuantity * 100m, 2, MidpointRounding.AwayFromZero);

        order.Status = order.Lines.All(l => l.DeliveredQuantity >= l.Quantity)
            ? SalesOrderStatus.Completed
            : SalesOrderStatus.PartiallyDelivered;
    }

    private static StockLedgerEntry NewLedgerEntry(
        DeliveryNotePostingLine line,
        Guid warehouseId,
        DeliveryNotePostingRequest request,
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

            // StockEntryId stays null: provenance rides on VoucherType/VoucherNo (Phase 4 decision -
            // the Kardex is fed by every stock-moving document, not only stock vouchers).
        };

    private static void AddGlLine(
        List<GLEntry> glLines,
        DeliveryNotePostingRequest request,
        Guid companyId,
        Account account,
        decimal debit,
        decimal credit,
        DeliveryNotePostingLine line,
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

            VoucherType = DeliveryVoucherType,

            // VoucherNo + VoucherId are stamped after the gapless number is assigned; the customer
            // party is stamped with them; no cost center module yet.
            VoucherNo = string.Empty,
            VoucherId = Guid.Empty,
            PartyType = null,
            PartyId = null,
            CostCenterId = null,
            IsCancelled = false,
            Remarks = $"DeliveryNote: {item.ItemCode} x{line.Qty:0.####}",
        });

    private static List<DeliveryNoteLine> BuildLines(
        DeliveryNotePostingRequest request,
        IReadOnlyDictionary<Guid, Item> items)
    {
        var lines = new List<DeliveryNoteLine>(request.Lines.Count);
        foreach (var line in request.Lines)
        {
            _ = items[line.ItemId];

            lines.Add(new DeliveryNoteLine
            {
                Id = Guid.NewGuid(),
                SalesOrderItemId = line.SalesOrderItemId,
                ItemId = line.ItemId,
                Qty = line.Qty,
            });
        }

        return lines;
    }

    // ------------------------------------------------------------------------------------ result

    private static DeliveryNotePostingDto BuildResult(
        DeliveryNote deliveryNote,
        IReadOnlyList<StockLedgerEntry> ledger,
        IReadOnlyList<GLEntry> glLines,
        SalesOrder order,
        Customer customer,
        IReadOnlyDictionary<Guid, Item> items)
    {
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

        return new DeliveryNotePostingDto(
            DeliveryNoteDto.Build(deliveryNote, items),
            ledgerDtos,
            glDtos,
            Round4(totalDebit),
            Round4(totalCredit),
            SalesOrderDto.Build(order, customer, items));
    }

    private static decimal Round4(decimal value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);
}
