using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using Erp.Domain.Services;

namespace Erp.Application.Services;

/// <summary>
/// The buying posting engine of Tasks 4.2/4.3. Responsibilities, in order, all inside one
/// transaction provided by <see cref="IPurchaseRepository.ExecuteInTransactionAsync{T}"/>:
/// validation -&gt; StockLedgerEntry rows (receipt) -&gt; balanced General Ledger lines -&gt; gapless
/// voucher number -&gt; save -&gt; workflow transition (Constitution III.1/III.4).
/// </summary>
/// <remarks>
/// GL mapping reproduces the numbered spec scenarios:
/// RECEIPT = Debit warehouse stock account / Credit Company.StockReceivedAccountCode (ST-01 and
/// tasks.md 4.2: Dr 1310, Cr 2120) while +Kardex rows value the incoming stock;
/// INVOICE = Debit StockReceivedAccountCode at RECEIPT value / Debit InputTaxRecoverable when
/// TaxAmount &gt; 0 / Debit-or-Credit PriceDifference when the billed rate differs / Credit
/// AccountsPayable for the gross (spec BY-01: Dr 2120 $1,000 + Dr tax $100 / Cr 2110 $1,100).
/// Company GL defaults are CODES resolved to exactly one active leaf account (decision D3).
/// </remarks>
public sealed class PurchasePostingService : IPurchasePostingService
{
    private const string ReceiptVoucherType = "PurchaseReceipt";
    private const string InvoiceVoucherType = "PurchaseInvoice";
    private const string ReceiptVoucherPrefix = "PR";
    private const string InvoiceVoucherPrefix = "PINV";

    private readonly ICompanyRepository _companies;
    private readonly IAccountRepository _accounts;
    private readonly IWarehouseRepository _warehouses;
    private readonly IItemRepository _items;
    private readonly IStockRepository _stock;
    private readonly IPurchaseRepository _purchases;

    public PurchasePostingService(
        ICompanyRepository companies,
        IAccountRepository accounts,
        IWarehouseRepository warehouses,
        IItemRepository items,
        IStockRepository stock,
        IPurchaseRepository purchases)
    {
        _companies = companies;
        _accounts = accounts;
        _warehouses = warehouses;
        _items = items;
        _stock = stock;
        _purchases = purchases;
    }

    // ------------------------------------------------------------------------ receipt (4.2)

    public async Task<PurchaseReceiptPostingDto> PostReceiptAsync(
        PurchaseReceiptPostingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        PurchaseValidator.EnsureHasLines(request.Lines);

        // One transaction for the WHOLE posting: validation, SLE, GL, voucher, save and the
        // workflow transition commit together or not at all (a rollback consumes no number).
        return await _purchases.ExecuteInTransactionAsync(async token =>
        {
            var company = await _companies.GetByIdAsync(request.CompanyId, token)
                ?? throw new StockValidationException(
                    StockErrorCodes.CompanyNotFound,
                    $"Company '{request.CompanyId}' was not found in this tenant.");

            var warehouse = await ResolveWarehouseAsync(request.WarehouseId, company.Id, token);
            var items = await LoadItemsAsync(request.Lines.Select(l => l.ItemId), token);

            // Resolve + sanity-check the GL accounts BEFORE any write (Constitution III.3).
            var stockAccount = await RequirePostableAccountAsync(
                warehouse.StockAccountId, company.Id, $"warehouse '{warehouse.Code}'", token);
            var receivedAccount = await RequireAccountByCodeAsync(
                company.Id,
                company.StockReceivedAccountCode,
                "Company.StockReceivedAccountCode",
                token);

            // Task 4.1 workflow: the referenced order must accept receipts (Draft/Billed refuse).
            PurchaseOrder? order = null;
            if (request.PurchaseOrderId is { } orderId)
            {
                order = await _purchases.GetOrderByIdAsync(orderId, token)
                    ?? throw new PurchaseValidationException(
                        PurchaseErrorCodes.PurchaseOrderNotFound,
                        $"Purchase order '{orderId}' was not found in this tenant.");

                if (order.CompanyId != company.Id)
                {
                    throw new PurchaseValidationException(
                        PurchaseErrorCodes.PurchaseOrderNotFound,
                        $"Purchase order '{order.VoucherNo}' does not belong to company '{company.Id}'.");
                }

                PurchaseValidator.EnsureReceiptAllowed(order.Status);
            }

            var ledger = new List<StockLedgerEntry>(request.Lines.Count);
            var glLines = new List<GLEntry>(request.Lines.Count * 2);

            foreach (var line in request.Lines)
            {
                PurchaseValidator.EnsureValidLine(line.Qty, line.Rate);
                var item = items[line.ItemId];
                var amount = Round4(line.Qty * line.Rate);

                ledger.Add(NewLedgerEntry(line, warehouse.Id, request, +line.Qty, line.Rate, +amount));

                // tasks.md 4.2: Debit warehouse stock account / Credit 2120 Stock Received But Not Billed.
                AddGlLine(
                    glLines, request.PostingDate, company.Id, ReceiptVoucherType, stockAccount,
                    debit: amount, credit: 0m,
                    remarks: $"PurchaseReceipt: {item.Code} x{line.Qty:0.####}");
                AddGlLine(
                    glLines, request.PostingDate, company.Id, ReceiptVoucherType, receivedAccount,
                    debit: 0m, credit: amount,
                    remarks: $"PurchaseReceipt: {item.Code} x{line.Qty:0.####}");
            }

            // Constitution III.1: balance must hold to four decimals BEFORE anything is saved.
            DoubleEntryGuard.EnsureBalanced(glLines);

            var receipt = new PurchaseReceipt
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                PurchaseOrderId = request.PurchaseOrderId,
                WarehouseId = warehouse.Id,
                PostingDate = request.PostingDate,
                VoucherNo = await _purchases.NextReceiptVoucherNumberAsync(
                    company.Id, ReceiptVoucherPrefix, request.PostingDate.Year, token),
                CreatedAt = DateTimeOffset.UtcNow,
                Lines = BuildReceiptLines(request, items),
            };

            // The Kardex rows and the GL lines share the voucher identity of this posting.
            foreach (var entry in ledger)
            {
                entry.VoucherType = ReceiptVoucherType;
                entry.VoucherNo = receipt.VoucherNo;
            }

            foreach (var glLine in glLines)
            {
                glLine.VoucherNo = receipt.VoucherNo;
            }

            await _purchases.AddReceiptAsync(receipt, token);
            await _stock.AddLedgerEntriesAsync(ledger, token);
            await _stock.AddGlEntriesAsync(glLines, token);

            if (order is not null)
            {
                order.Status = PurchaseOrderStatus.Received;
                await _purchases.UpdateOrderAsync(order, token);
            }

            return BuildReceiptResult(receipt, ledger, glLines, items, order?.Status);
        }, cancellationToken);
    }

    // ------------------------------------------------------------------------ invoice (4.3)

    public async Task<PurchaseInvoicePostingDto> PostInvoiceAsync(
        PurchaseInvoicePostingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        PurchaseValidator.EnsureHasLines(request.Lines);
        PurchaseValidator.EnsureValidTaxAmount(request.TaxAmount);

        return await _purchases.ExecuteInTransactionAsync(async token =>
        {
            var company = await _companies.GetByIdAsync(request.CompanyId, token)
                ?? throw new StockValidationException(
                    StockErrorCodes.CompanyNotFound,
                    $"Company '{request.CompanyId}' was not found in this tenant.");

            var receipt = await _purchases.GetReceiptByIdAsync(request.PurchaseReceiptId, token)
                ?? throw new PurchaseValidationException(
                    PurchaseErrorCodes.PurchaseReceiptNotFound,
                    $"Purchase receipt '{request.PurchaseReceiptId}' was not found in this tenant.");

            if (receipt.CompanyId != company.Id)
            {
                throw new PurchaseValidationException(
                    PurchaseErrorCodes.PurchaseReceiptNotFound,
                    $"Purchase receipt '{receipt.VoucherNo}' does not belong to company '{company.Id}'.");
            }

            // Task 4.3: ONE invoice per receipt (the unique index is the hard backstop).
            if (await _purchases.ReceiptHasInvoiceAsync(receipt.Id, token))
            {
                throw new PurchaseValidationException(
                    PurchaseErrorCodes.InvoiceAlreadyExists,
                    $"Purchase receipt '{receipt.VoucherNo}' already has a purchase invoice.");
            }

            var receiptLinesById = receipt.Lines.ToDictionary(l => l.Id);
            var items = await LoadItemsAsync(request.Lines.Select(l => l.ItemId), token);

            // --- three-way full match (v1): bill every receipt line exactly at its quantity.
            var billedReceiptLineIds = new HashSet<Guid>();
            foreach (var line in request.Lines)
            {
                PurchaseValidator.EnsureValidInvoiceLine(line.Qty, line.Rate);

                if (!receiptLinesById.TryGetValue(line.PurchaseReceiptLineId, out var receiptLine))
                {
                    throw new PurchaseValidationException(
                        PurchaseErrorCodes.ReceiptLineMismatch,
                        $"Invoice line references receipt line '{line.PurchaseReceiptLineId}', "
                        + $"which does not belong to receipt '{receipt.VoucherNo}'.");
                }

                if (!billedReceiptLineIds.Add(line.PurchaseReceiptLineId))
                {
                    throw new PurchaseValidationException(
                        PurchaseErrorCodes.ReceiptLineMismatch,
                        $"Receipt line '{line.PurchaseReceiptLineId}' is billed more than once inside the same invoice.");
                }

                if (line.ItemId != receiptLine.ItemId)
                {
                    throw new PurchaseValidationException(
                        PurchaseErrorCodes.ReceiptLineMismatch,
                        $"Invoice line item '{line.ItemId}' does not match receipt line item '{receiptLine.ItemId}'.");
                }

                if (line.Qty != receiptLine.Qty)
                {
                    throw new PurchaseValidationException(
                        PurchaseErrorCodes.QuantityMismatch,
                        $"Invoice quantity {line.Qty:0.####} must equal received quantity "
                        + $"{receiptLine.Qty:0.####} for item '{items[line.ItemId].Code}' (full three-way match).");
                }
            }

            if (billedReceiptLineIds.Count != receiptLinesById.Count)
            {
                var missing = receiptLinesById.Keys.First(k => !billedReceiptLineIds.Contains(k));
                throw new PurchaseValidationException(
                    PurchaseErrorCodes.ReceiptLineMismatch,
                    $"Every receipt line must be billed: receipt line '{missing}' of "
                    + $"'{receipt.VoucherNo}' is not covered.");
            }

            // --- GL accounts (Constitution III.3 sanity BEFORE any write).
            var receivedAccount = await RequireAccountByCodeAsync(
                company.Id, company.StockReceivedAccountCode, "Company.StockReceivedAccountCode", token);
            var payableAccount = await RequireAccountByCodeAsync(
                company.Id, company.AccountsPayableAccountCode, "Company.AccountsPayableAccountCode", token);

            // --- per line: clear the interim liability at RECEIPT value, accumulate the payable
            // at billed value (the delta per line becomes the price difference account).
            var glLines = new List<GLEntry>(request.Lines.Count + 3);
            var interimTotal = 0m;
            var payableTotal = 0m;

            foreach (var line in request.Lines)
            {
                var receiptLine = receiptLinesById[line.PurchaseReceiptLineId];
                var item = items[line.ItemId];
                var interim = Round4(line.Qty * receiptLine.Rate);
                var payable = Round4(line.Qty * line.Rate);

                interimTotal += interim;
                payableTotal += payable;

                // tasks.md 4.3: debit the accrual for the billed quantities (zeroes it out).
                AddGlLine(
                    glLines, request.PostingDate, company.Id, InvoiceVoucherType, receivedAccount,
                    debit: interim, credit: 0m,
                    remarks: $"PurchaseInvoice: {item.Code} x{line.Qty:0.####} accrual clearance");
            }

            var taxAmount = Round4(request.TaxAmount);
            if (taxAmount > 0)
            {
                var taxAccount = await RequireAccountByCodeAsync(
                    company.Id,
                    company.InputTaxRecoverableAccountCode,
                    "Company.InputTaxRecoverableAccountCode",
                    token);

                // spec BY-01: Debit Input Tax Recoverable.
                AddGlLine(
                    glLines, request.PostingDate, company.Id, InvoiceVoucherType, taxAccount,
                    debit: taxAmount, credit: 0m,
                    remarks: "PurchaseInvoice: input tax recoverable");
            }

            var variance = Round4(payableTotal - interimTotal);
            if (variance != 0)
            {
                var priceDifferenceAccount = await RequireAccountByCodeAsync(
                    company.Id,
                    company.PriceDifferenceAccountCode,
                    "Company.PriceDifferenceAccountCode",
                    token);

                AddGlLine(
                    glLines, request.PostingDate, company.Id, InvoiceVoucherType, priceDifferenceAccount,
                    debit: variance > 0 ? variance : 0m,
                    credit: variance < 0 ? -variance : 0m,
                    remarks: variance > 0
                        ? "PurchaseInvoice: price difference (billed above received)"
                        : "PurchaseInvoice: price difference (billed below received)");
            }

            // Gross payable: net billed + Input Tax (spec BY-01: Cr Accounts Payable $1,100.00).
            AddGlLine(
                glLines, request.PostingDate, company.Id, InvoiceVoucherType, payableAccount,
                debit: 0m, credit: Round4(payableTotal + taxAmount),
                remarks: "PurchaseInvoice: accounts payable");

            // Constitution III.1: balance must hold to four decimals BEFORE anything is saved.
            DoubleEntryGuard.EnsureBalanced(glLines);

            var invoice = new PurchaseInvoice
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                PurchaseReceiptId = receipt.Id,
                PostingDate = request.PostingDate,
                TaxAmount = taxAmount,
                VoucherNo = await _purchases.NextInvoiceVoucherNumberAsync(
                    company.Id, InvoiceVoucherPrefix, request.PostingDate.Year, token),
                CreatedAt = DateTimeOffset.UtcNow,
                Lines = BuildInvoiceLines(request, items),
            };

            foreach (var glLine in glLines)
            {
                glLine.VoucherNo = invoice.VoucherNo;
            }

            await _purchases.AddInvoiceAsync(invoice, token);
            await _stock.AddGlEntriesAsync(glLines, token);

            // Task 4.1 workflow: billing the receipt closes the order.
            var order = receipt.PurchaseOrder;
            if (order is not null)
            {
                order.Status = PurchaseOrderStatus.Billed;
                await _purchases.UpdateOrderAsync(order, token);
            }

            return BuildInvoiceResult(invoice, glLines, items, order?.Status);
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
                $"Warehouse '{warehouse.Code}' does not belong to company '{companyId}'.");
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
            throw new PurchasePostingConfigurationException(
                $"No stock account is linked to {ownerContext}; link an active leaf account before posting stock.");
        }

        var account = await _accounts.GetByIdAsync(accountId, token)
            ?? throw new PurchasePostingConfigurationException(
                $"The stock account '{accountId}' linked to {ownerContext} does not exist.");

        EnsurePostable(account, companyId, ownerContext);
        return account;
    }

    private static void EnsurePostable(Account account, Guid companyId, string ownerContext)
    {
        if (!account.IsActive)
        {
            throw new PurchaseValidationException(
                PurchaseErrorCodes.InvalidGlAccount,
                $"Account '{account.AccountCode}' ({ownerContext}) is inactive and cannot receive General Ledger postings.");
        }

        if (account.IsGroup)
        {
            throw new PurchaseValidationException(
                PurchaseErrorCodes.InvalidGlAccount,
                $"Account '{account.AccountCode}' ({ownerContext}) is a group account and cannot receive General Ledger postings.");
        }

        if (account.CompanyId != companyId)
        {
            throw new PurchaseValidationException(
                PurchaseErrorCodes.InvalidGlAccount,
                $"Account '{account.AccountCode}' belongs to another company and cannot be posted from this company.");
        }
    }

    /// <summary>
    /// Decision D3 applied to the buying defaults: stored as account CODES (a Company -&gt; Account
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
            throw new PurchasePostingConfigurationException(
                $"Company '{companyId}' does not configure {settingName}. "
                + "Seed it with an active leaf account code before posting.");
        }

        var matches = await _accounts.FindActiveLeafByCodeAsync(companyId, accountCode, token);

        if (matches.Count == 0)
        {
            throw new PurchasePostingConfigurationException(
                $"{settingName} = '{accountCode}' does not resolve to an ACTIVE LEAF account of company "
                + $"'{companyId}'. Seed the account (IsActive = 1, IsGroup = 0) or fix the company setting.");
        }

        if (matches.Count > 1)
        {
            throw new PurchasePostingConfigurationException(
                $"{settingName} = '{accountCode}' is ambiguous: {matches.Count} active leaf accounts share that "
                + $"code in company '{companyId}'. Account codes must be unique per company.");
        }

        return matches[0];
    }

    // ------------------------------------------------------------------------------- persistence

    private static StockLedgerEntry NewLedgerEntry(
        PurchaseReceiptPostingLine line,
        Guid warehouseId,
        PurchaseReceiptPostingRequest request,
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
        DateOnly postingDate,
        Guid companyId,
        string voucherType,
        Account account,
        decimal debit,
        decimal credit,
        string remarks) =>
        glLines.Add(new GLEntry
        {
            CompanyId = companyId,
            PostingDate = postingDate,
            AccountId = account.Id,

            // Navigation kept populated so the API response can show account code + name without
            // a second round trip (EF fixes up the FK from the reference anyway).
            Account = account,
            Debit = Round4(debit),
            Credit = Round4(credit),
            VoucherType = voucherType,

            // VoucherNo is stamped after the gapless number is assigned (same transaction).
            VoucherNo = string.Empty,
            Remarks = remarks,
        });

    private static List<PurchaseReceiptLine> BuildReceiptLines(
        PurchaseReceiptPostingRequest request,
        IReadOnlyDictionary<Guid, Item> items)
    {
        var lines = new List<PurchaseReceiptLine>(request.Lines.Count);
        for (var i = 0; i < request.Lines.Count; i++)
        {
            var line = request.Lines[i];
            _ = items[line.ItemId];

            lines.Add(new PurchaseReceiptLine
            {
                Id = Guid.NewGuid(),
                ItemId = line.ItemId,
                Qty = line.Qty,
                Rate = line.Rate,
                LineNumber = i + 1,
            });
        }

        return lines;
    }

    private static List<PurchaseInvoiceLine> BuildInvoiceLines(
        PurchaseInvoicePostingRequest request,
        IReadOnlyDictionary<Guid, Item> items)
    {
        var lines = new List<PurchaseInvoiceLine>(request.Lines.Count);
        for (var i = 0; i < request.Lines.Count; i++)
        {
            var line = request.Lines[i];
            _ = items[line.ItemId];

            lines.Add(new PurchaseInvoiceLine
            {
                Id = Guid.NewGuid(),
                PurchaseReceiptLineId = line.PurchaseReceiptLineId,
                ItemId = line.ItemId,
                Qty = line.Qty,
                Rate = line.Rate,
                LineNumber = i + 1,
            });
        }

        return lines;
    }

    // ------------------------------------------------------------------------------------ result

    private static PurchaseReceiptPostingDto BuildReceiptResult(
        PurchaseReceipt receipt,
        IReadOnlyList<StockLedgerEntry> ledger,
        IReadOnlyList<GLEntry> glLines,
        IReadOnlyDictionary<Guid, Item> items,
        PurchaseOrderStatus? orderStatus)
    {
        var dto = PurchaseReceiptDto.Build(receipt, items);

        var ledgerDtos = ledger
            .Select(e => new StockLedgerEntryDto(e.Id, e.ItemId, e.WarehouseId, e.PostingDate, e.QtyChange, e.ValuationRate, e.Amount))
            .ToList();

        var (totalDebit, totalCredit, glDtos) = BuildGlDtos(glLines);
        return new PurchaseReceiptPostingDto(dto, ledgerDtos, glDtos, totalDebit, totalCredit, orderStatus);
    }

    private static PurchaseInvoicePostingDto BuildInvoiceResult(
        PurchaseInvoice invoice,
        IReadOnlyList<GLEntry> glLines,
        IReadOnlyDictionary<Guid, Item> items,
        PurchaseOrderStatus? orderStatus)
    {
        var dto = PurchaseInvoiceDto.Build(invoice, items);

        var (totalDebit, totalCredit, glDtos) = BuildGlDtos(glLines);
        return new PurchaseInvoicePostingDto(dto, glDtos, totalDebit, totalCredit, orderStatus);
    }

    private static (decimal TotalDebit, decimal TotalCredit, List<GLEntryDto> GlDtos) BuildGlDtos(
        IReadOnlyList<GLEntry> glLines)
    {
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

        return (Round4(totalDebit), Round4(totalCredit), glDtos);
    }

    private static decimal Round4(decimal value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);
}
