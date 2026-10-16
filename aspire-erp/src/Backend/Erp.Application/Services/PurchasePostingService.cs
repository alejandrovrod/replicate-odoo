using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.Assets;
using Erp.Domain.Common;
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

    /// <summary>Module 19 (T1): gapless prefix for supplier debit notes (purchase returns).</summary>
    private const string ReturnVoucherPrefix = "PRET";

    /// <summary>plan.md §2 GLEntry.PartyType vocabulary value for supplier parties.</summary>
    private const string SupplierPartyType = "Supplier";

    private readonly ICompanyRepository _companies;
    private readonly IAccountRepository _accounts;
    private readonly IWarehouseRepository _warehouses;
    private readonly IItemRepository _items;
    private readonly IStockRepository _stock;
    private readonly IPurchaseRepository _purchases;
    private readonly IExchangeRateRepository _exchangeRates;
    private readonly IAssetsRepository _assets;
    private readonly IEnumerable<IDocumentLifecycleHook<PurchaseInvoice>>? _invoiceHooks;
    private readonly ITaxEngineService _taxEngine;
    private readonly ITaxTemplateRepository? _taxTemplates;
    private readonly IBatchSerialService? _tracking;

    public PurchasePostingService(
        ICompanyRepository companies,
        IAccountRepository accounts,
        IWarehouseRepository warehouses,
        IItemRepository items,
        IStockRepository stock,
        IPurchaseRepository purchases,
        IExchangeRateRepository exchangeRates,
        IAssetsRepository assets,
        IEnumerable<IDocumentLifecycleHook<PurchaseInvoice>>? invoiceHooks = null,
        ITaxEngineService? taxEngine = null,
        ITaxTemplateRepository? taxTemplates = null,
        IBatchSerialService? tracking = null)
    {
        _companies = companies;
        _accounts = accounts;
        _warehouses = warehouses;
        _items = items;
        _stock = stock;
        _purchases = purchases;
        _exchangeRates = exchangeRates;
        _assets = assets;
        _invoiceHooks = invoiceHooks;
        _taxEngine = taxEngine ?? new TaxEngineService();
        _taxTemplates = taxTemplates;
        _tracking = tracking;
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

            // tasks.md 2.2 / spec AC-04: hard fiscal period lock - FIRST check, before any
            // GLEntry/StockLedgerEntry line is built, so a back-dated receipt modifies zero data.
            company.EnsurePostingDateUnlocked(request.PostingDate);
            // R-13 FC-04: closed fiscal year rejects the posting too (second half of plan.md §3).
            await _companies.EnsurePostingDateInOpenYearAsync(company.Id, request.PostingDate, token);

            var warehouse = await ResolveWarehouseAsync(request.WarehouseId, company.Id, token);
            var items = await LoadItemsAsync(request.Lines.Select(l => l.ItemId), token);

            // Resolve + sanity-check the GL accounts BEFORE any write (Constitution III.3).
            var stockAccount = await RequirePostableAccountAsync(
                warehouse.AccountId ?? Guid.Empty, company.Id, $"warehouse '{warehouse.WarehouseCode}'", token);
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
                        $"Purchase order '{order.OrderNumber}' does not belong to company '{company.Id}'.");
                }

                if (order.SupplierId != request.SupplierId)
                {
                    throw new PurchaseValidationException(
                        PurchaseErrorCodes.PurchaseOrderNotFound,
                        $"Receipt supplier '{request.SupplierId}' does not match order supplier '{order.SupplierId}'.");
                }

                PurchaseValidator.EnsureReceiptAllowed(order.Status);
            }

            var ledger = new List<StockLedgerEntry>(request.Lines.Count);
            var glLines = new List<GLEntry>(request.Lines.Count * 2);

            // Batch-and-serial Fase 3: per-line batch resolution + staged receipt serials,
            // persisted with the voucher after numbering.
            var batchByLine = new Dictionary<int, Guid?>();
            var serialsByLine = new Dictionary<int, List<SerialNo>>();

            foreach (var (line, lineIndex) in request.Lines.Select((l, i) => (l, i)))
            {
                PurchaseValidator.EnsureValidLine(line.Qty, line.Rate);
                var item = items[line.ItemId];
                var amount = Round4(line.Qty * line.Rate);

                var batchId = await PlanReceiptTrackingAsync(
                    request, line, item, warehouse, lineIndex,
                    batchByLine, serialsByLine, token);

                ledger.Add(NewLedgerEntry(line, warehouse.Id, request, +line.Qty, line.Rate, +amount, batchId));

                // tasks.md 4.2: Debit warehouse stock account / Credit 2120 Stock Received But Not Billed.
                AddGlLine(
                    glLines, request.PostingDate, company.Id, ReceiptVoucherType, stockAccount,
                    debit: amount, credit: 0m,
                    remarks: $"PurchaseReceipt: {item.ItemCode} x{line.Qty:0.####}");
                AddGlLine(
                    glLines, request.PostingDate, company.Id, ReceiptVoucherType, receivedAccount,
                    debit: 0m, credit: amount,
                    remarks: $"PurchaseReceipt: {item.ItemCode} x{line.Qty:0.####}");
            }

            // Constitution III.1: balance must hold to four decimals BEFORE anything is saved.
            DoubleEntryGuard.EnsureBalanced(glLines);

            var receipt = new PurchaseReceipt
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                SupplierId = request.SupplierId,
                PurchaseOrderId = request.PurchaseOrderId,
                WarehouseId = warehouse.Id,
                PostingDate = request.PostingDate,
                VoucherNo = await _purchases.NextReceiptVoucherNumberAsync(
                    company.Id, ReceiptVoucherPrefix, request.PostingDate.Year, token),
                Status = PurchaseReceiptStatus.Submitted,
                TotalAmount = request.Lines.Sum(l => Round4(l.Qty * l.Rate)),
                CreatedAt = DateTimeOffset.UtcNow,
                Lines = BuildReceiptLines(request, items, batchByLine),
            };

            // The Kardex rows and the GL lines share the voucher identity of this posting.
            foreach (var entry in ledger)
            {
                entry.VoucherType = ReceiptVoucherType;
                entry.VoucherNo = receipt.VoucherNo;
            }

            // plan.md §2 voucher provenance: the receipt aggregate is the source document; the
            // supplier party rides on the GL lines.
            foreach (var glLine in glLines)
            {
                glLine.VoucherNo = receipt.VoucherNo;
                glLine.VoucherId = receipt.Id;
                glLine.PartyType = SupplierPartyType;
                glLine.PartyId = request.SupplierId;
            }

            await _purchases.AddReceiptAsync(receipt, token);
            await _stock.AddLedgerEntriesAsync(ledger, token);
            await _stock.AddGlEntriesAsync(glLines, token);

            // Batch-and-serial Fase 3: stamp the purchase document refs on staged receipt
            // serials and persist them with everything else, atomically.
            if (serialsByLine.Count > 0 && _tracking is not null)
            {
                var toAdd = serialsByLine.Values.SelectMany(s => s).ToList();
                foreach (var serial in toAdd)
                {
                    serial.PurchaseDocumentNo = receipt.VoucherNo;
                }

                await _tracking.PersistSerialsAsync(toAdd, Array.Empty<SerialNo>(), token);
            }

            if (order is not null)
            {
                // Task 4.5 progress columns (verify W5): the receipt advances the linked order's
                // per-line received quantities and the header percentage before the workflow step.
                ApplyOrderProgress(
                    order,
                    request.Lines.Select(l => (l.ItemId, l.Qty)),
                    received: true);
                order.Status = PurchaseOrderStatus.PartiallyReceived;
                await _purchases.UpdateOrderAsync(order, token);
            }

            return BuildReceiptResult(receipt, ledger, glLines, items, order?.Status);
        }, cancellationToken);
    }

    /// <summary>
    /// Batch-and-serial Fase 3 receipt planning: shape validation plus batch top-up-or-create
    /// and serial staging. Returns the resolved batch id (null for untracked items).
    /// </summary>
    private async Task<Guid?> PlanReceiptTrackingAsync(
        PurchaseReceiptPostingRequest request,
        PurchaseReceiptPostingLine line,
        Item item,
        Warehouse warehouse,
        int lineIndex,
        Dictionary<int, Guid?> batchByLine,
        Dictionary<int, List<SerialNo>> serialsByLine,
        CancellationToken token)
    {
        var cleanedSerials = StockEntryValidator.EnsureTrackingShape(
            item.ItemCode,
            item.HasBatchNo,
            batchId: null,
            line.BatchNo,
            isReceipt: true,
            item.HasSerialNo,
            line.SerialNumbers,
            line.Qty);

        if (!item.HasBatchNo && !item.HasSerialNo)
        {
            batchByLine[lineIndex] = null;
            return null;
        }

        if (_tracking is null)
        {
            throw new StockValidationException(
                StockErrorCodes.BatchRequired,
                $"Item '{item.ItemCode}' is batch/serial-tracked but no tracking service is wired.");
        }

        Guid? batchId = null;
        if (item.HasBatchNo)
        {
            var batch = await _tracking.EnsureBatchForReceiptAsync(
                item.Id, item.ItemCode, line.BatchNo!, line.ManufacturingDate, line.ExpiryDate, token);
            batchId = batch.Id;
        }

        batchByLine[lineIndex] = batchId;

        if (item.HasSerialNo)
        {
            var staged = await _tracking.StageReceiptSerialsAsync(
                item.Id, item.ItemCode, cleanedSerials, warehouse.Id, batchId,
                request.PostingDate, token);
            serialsByLine[lineIndex] = staged.ToList();
        }

        return batchId;
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

            // tasks.md 2.2 / spec AC-04: hard fiscal period lock - FIRST check, before any
            // GLEntry line is built, so a back-dated invoice modifies zero data.
            company.EnsurePostingDateUnlocked(request.PostingDate);
            // R-13 FC-04: closed fiscal year rejects the posting too (second half of plan.md §3).
            await _companies.EnsurePostingDateInOpenYearAsync(company.Id, request.PostingDate, token);

            var receiptLineIds = request.Lines.Select(l => l.PurchaseReceiptLineId).Distinct().ToList();
            var receiptLines = await _purchases.GetReceiptLinesByIdsAsync(receiptLineIds, token);
            var receiptLinesById = receiptLines.ToDictionary(l => l.Id);

            var items = await LoadItemsAsync(request.Lines.Select(l => l.ItemId), token);
            var receipts = receiptLines.Select(l => l.PurchaseReceipt!).DistinctBy(r => r.Id).ToList();

            foreach (var receipt in receipts)
            {
                if (receipt.CompanyId != company.Id)
                {
                    throw new PurchaseValidationException(
                        PurchaseErrorCodes.PurchaseReceiptNotFound,
                        $"Purchase receipt '{receipt.VoucherNo}' does not belong to company '{company.Id}'.");
                }
                
                if (receipt.SupplierId != request.SupplierId)
                {
                    throw new PurchaseValidationException(
                        PurchaseErrorCodes.PurchaseReceiptNotFound,
                        $"Purchase receipt '{receipt.VoucherNo}' supplier '{receipt.SupplierId}' does not match invoice supplier '{request.SupplierId}'.");
                }
            }

            var billedReceiptLineIds = new HashSet<Guid>();
            foreach (var line in request.Lines)
            {
                PurchaseValidator.EnsureValidInvoiceLine(line.Qty, line.Rate);

                if (!receiptLinesById.TryGetValue(line.PurchaseReceiptLineId, out var receiptLine))
                {
                    throw new PurchaseValidationException(
                        PurchaseErrorCodes.ReceiptLineMismatch,
                        $"Invoice line references receipt line '{line.PurchaseReceiptLineId}', which was not found.");
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

                var previouslyBilledQty = await _purchases.GetBilledQuantityForReceiptLineAsync(line.PurchaseReceiptLineId, token);
                ThreeWayMatchValidator.ValidateBillingQuantity(receiptLine.Qty, previouslyBilledQty, line.Qty);
            }

            var allReceiptLineIds = receipts.SelectMany(r => r.Lines).Select(l => l.Id).ToHashSet();
            if (billedReceiptLineIds.Count != allReceiptLineIds.Count)
            {
                throw new PurchaseValidationException(
                    PurchaseErrorCodes.ReceiptLineMismatch,
                    "Invoice must cover all receipt lines exactly (full three-way match).");
            }

            // Verify W9: one vendor bill reference books ONCE per company - the friendly path of
            // the IX_PurchaseInvoice_Company_BillNumber rule (the index remains the race backstop).
            // Blank references stay legal and do not collide (the index is filtered).
            if (!string.IsNullOrWhiteSpace(request.BillNumber)
                && await _purchases.InvoiceBillNumberExistsAsync(company.Id, request.BillNumber, token))
            {
                throw new PurchaseValidationException(
                    PurchaseErrorCodes.InvoiceAlreadyExists,
                    $"An invoice with bill number '{request.BillNumber}' already exists for this company.");
            }

            // --- FX Resolution
            var exchangeRate = 1m;
            if (request.CurrencyId.HasValue && company.CurrencyId.HasValue && request.CurrencyId != company.CurrencyId)
            {
                var rateRow = await _exchangeRates.GetLatestRateAsync(request.CurrencyId.Value, company.CurrencyId.Value, request.PostingDate, token);
                if (rateRow == null)
                {
                    throw new PurchaseValidationException(FxErrorCodes.ExchangeRateMissing, $"Missing exchange rate for {request.CurrencyId.Value}");
                }
                exchangeRate = rateRow.Rate;
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
            var assetPlans = new List<AssetLinePlan>();

            for (var index = 0; index < request.Lines.Count; index++)
            {
                var line = request.Lines[index];
                var receiptLine = receiptLinesById[line.PurchaseReceiptLineId];
                var item = items[line.ItemId];
                var interim = Round4(line.Qty * receiptLine.Rate);
                var payable = Round4(line.Qty * line.Rate);

                interimTotal += interim;
                payableTotal += payable;

                // Asset-capitalization module (gap D1): fixed-asset lines need a category, its cost
                // account and the source warehouse's stock account resolved BEFORE any write.
                if (item.IsFixedAsset)
                {
                    assetPlans.Add(await PlanAssetCapitalizationAsync(
                        index, line, receiptLine, item, company.Id, exchangeRate, token));
                }

                // tasks.md 4.3: debit the accrual for the billed quantities (zeroes it out).
                AddGlLine(
                    glLines, request.PostingDate, company.Id, InvoiceVoucherType, receivedAccount,
                    debit: interim * exchangeRate, credit: 0m,
                    debitInFC: interim, creditInFC: 0m,
                    remarks: $"PurchaseInvoice: {item.ItemCode} x{line.Qty:0.####} accrual clearance");
            }

            var taxAmount = Round4(request.TaxAmount);
            // Tax-engine Fase 2: explicit rows (template + manual) or the legacy scalar.
            // Legacy (no rows, no template): ONE FixedAmount Add row against the input-tax
            // account - the engine reproduces the scalar verbatim, so the GL below is
            // byte-identical to the pre-engine posting (spec BY-01 unchanged).
            var taxPlan = await ResolvePurchaseTaxRowsAsync(request, company, token);
            var taxCalc = _taxEngine.Calculate(payableTotal, taxPlan.Rows, decimals: 4);
            taxAmount = Round4(taxCalc.TaxTotal);
            var withholdingTotal = Round4(taxCalc.DeductTotal);

            for (var taxIndex = 0; taxIndex < taxCalc.Rows.Count; taxIndex++)
            {
                var computed = taxCalc.Rows[taxIndex];
                var taxAccount = taxPlan.Accounts[taxIndex];
                if (computed.TaxAmount == 0m)
                {
                    continue;
                }

                // Legacy scalar rows keep their historical remark verbatim so the pre-engine
                // GL is byte-identical (spec BY-01 regression anchor).
                var remarks = taxPlan.Remarks[taxIndex];
                if (computed.TaxAmount > 0m)
                {
                    // Transferred input tax: Debit the row's account (recoverable asset,
                    // non-recoverable expense pass-through, ...).
                    AddGlLine(
                        glLines, request.PostingDate, company.Id, InvoiceVoucherType, taxAccount,
                        debit: computed.TaxAmount * exchangeRate, credit: 0m,
                        debitInFC: computed.TaxAmount, creditInFC: 0m,
                        remarks: remarks ?? $"PurchaseInvoice: input tax ({computed.ChargeType} {computed.Rate:0.##}%)");
                }
                else
                {
                    // Fiscal withholding suffered on the bill: Credit the row's account
                    // (withholding payable to the tax authority).
                    var magnitude = -computed.TaxAmount;
                    AddGlLine(
                        glLines, request.PostingDate, company.Id, InvoiceVoucherType, taxAccount,
                        debit: 0m, credit: magnitude * exchangeRate,
                        debitInFC: 0m, creditInFC: magnitude,
                        remarks: remarks ?? $"PurchaseInvoice: withholding ({computed.ChargeType} {computed.Rate:0.##}%)");
                }
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
                    debit: variance > 0 ? (variance * exchangeRate) : 0m,
                    credit: variance < 0 ? (-variance * exchangeRate) : 0m,
                    debitInFC: variance > 0 ? variance : 0m,
                    creditInFC: variance < 0 ? -variance : 0m,
                    remarks: variance > 0
                        ? "PurchaseInvoice: price difference (billed above received)"
                        : "PurchaseInvoice: price difference (billed below received)");
            }

            var grossTotal = Round4(payableTotal + taxAmount);
            
            // Gross payable: net billed + Input Tax (spec BY-01: Cr Accounts Payable $1,100.00).
            AddGlLine(
                glLines, request.PostingDate, company.Id, InvoiceVoucherType, payableAccount,
                debit: 0m, credit: grossTotal * exchangeRate,
                debitInFC: 0m, creditInFC: grossTotal,
                remarks: "PurchaseInvoice: accounts payable");

            // Asset capitalization (asset-capitalization module, gap D3): each capitalized line is
            // reclassified OUT of the warehouse stock account into the category's cost account. The
            // receipt had debited stock; this bill cleared the interim liability; the reclass lands
            // the value in the fixed-asset account, so the net chain is Dr Fixed Asset / Cr Payable.
            // The pair is self-balancing, so the invoice keeps its zero-sum invariant.
            foreach (var plan in assetPlans)
            {
                AddGlLine(
                    glLines, request.PostingDate, company.Id, InvoiceVoucherType, plan.CostAccount,
                    debit: plan.BilledCompany, credit: 0m,
                    debitInFC: plan.BilledFc, creditInFC: 0m,
                    remarks: $"PurchaseInvoice: capitalize {plan.Item.ItemCode} x{plan.Quantity:0.####} into fixed assets");
                AddGlLine(
                    glLines, request.PostingDate, company.Id, InvoiceVoucherType, plan.StockAccount,
                    debit: 0m, credit: plan.BilledCompany,
                    debitInFC: 0m, creditInFC: plan.BilledFc,
                    remarks: $"PurchaseInvoice: reclassify {plan.Item.ItemCode} from stock to fixed asset");
            }

            // Constitution III.1: balance must hold to four decimals BEFORE anything is saved.
            DoubleEntryGuard.EnsureBalanced(glLines);

            var invoice = new PurchaseInvoice
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                SupplierId = request.SupplierId,
                BillNumber = request.BillNumber,
                PostingDate = request.PostingDate,
                DueDate = request.DueDate,
                // Spec BY-05: a posted bill is born Unpaid so that cancellation has a
                // valid transition (Unpaid -> Cancelled) to perform.
                Status = PurchaseInvoiceStatus.Unpaid,
                CurrencyId = request.CurrencyId,
                ExchangeRate = exchangeRate,
                NetTotal = payableTotal,
                TaxTotal = taxAmount,
                WithholdingTaxTotal = withholdingTotal,
                GrandTotal = grossTotal,
                OutstandingAmount = grossTotal,
                VoucherNo = await _purchases.NextInvoiceVoucherNumberAsync(
                    company.Id, InvoiceVoucherPrefix, request.PostingDate.Year, token),
                CreatedAt = DateTimeOffset.UtcNow,
                Lines = BuildInvoiceLines(request, items),
                // Tax-engine Fase 2: explicit rows persist with their bases (the UI breakdown
                // and audit read them back); the legacy scalar path persists zero rows.
                Taxes = taxPlan.Persist
                    ? taxCalc.Rows.Select((r, i) => new PurchaseInvoiceTax
                    {
                        Id = Guid.NewGuid(),
                        AccountId = taxPlan.Accounts[i].Id,
                        Rate = taxPlan.Rates[i],
                        ChargeType = r.ChargeType,
                        CalculationBasis = r.CalculationBasis,
                        FixedAmount = taxPlan.FixedAmounts[i],
                        BaseAmount = r.BaseAmount,
                        TaxAmount = r.TaxAmount,
                    }).ToList()
                    : new List<PurchaseInvoiceTax>(),
            };

            // plan.md §2 voucher provenance: the invoice aggregate is the source document; the
            // supplier party rides on the GL lines.
            foreach (var glLine in glLines)
            {
                glLine.VoucherNo = invoice.VoucherNo;
                glLine.VoucherId = invoice.Id;
                glLine.PartyType = SupplierPartyType;
                glLine.PartyId = request.SupplierId;
            }

            // Extensibility Fase 1 (gap D3): Before hooks run after the Core validation/GL and
            // BEFORE the first write (AddInvoiceAsync). A rejection aborts inside the
            // transaction: zero invoice, zero GL (same guarantee as the sales submit path).
            await DocumentLifecycleInvoker.RunBeforeSubmitAsync(_invoiceHooks, invoice, token);

            await _purchases.AddInvoiceAsync(invoice, token);
            await _stock.AddGlEntriesAsync(glLines, token);

            // Asset-capitalization module (gap D1/D4): each fixed-asset line becomes an Asset linked
            // to this bill and its line, with the gapless AST code and the straight-line schedule
            // generated inside the SAME transaction. The invoice is already persisted, so the line
            // ids exist and EF inserts the assets without FK-ordering ambiguity.
            if (assetPlans.Count > 0)
            {
                var invoiceLines = invoice.Lines.OrderBy(l => l.LineNumber).ToList();
                foreach (var plan in assetPlans)
                {
                    await CreateAssetFromInvoiceLineAsync(
                        plan, invoice, invoiceLines[plan.Index], request.PostingDate, company.Id, token);
                }
            }

            // Task 4.1 workflow (verify W5/W6): billing advances the linked orders' per-line billed
            // quantities and header percentage, and an order completes ONLY when every line is
            // fully billed - a partial bill keeps it PartiallyReceived instead of faking a close.
            var ordersToUpdate = receipts
                .Select(r => r.PurchaseOrder)
                .Where(o => o is not null && o.Status != PurchaseOrderStatus.Completed)
                .DistinctBy(o => o!.Id)
                .ToList();

            foreach (var order in ordersToUpdate)
            {
                // Re-load with Include(Items): the receipt navigation carries the header only. EF
                // resolves to the SAME tracked instance (the fake returns the same object), so the
                // mutation below lands on the aggregate that UpdateOrderAsync persists.
                var loaded = await _purchases.GetOrderByIdAsync(order!.Id, token) ?? order;

                var billedLines = request.Lines
                    .Where(l =>
                        receiptLinesById[l.PurchaseReceiptLineId].PurchaseReceipt!.PurchaseOrderId
                        == loaded.Id)
                    .Select(l => (l.ItemId, l.Qty));

                ApplyOrderProgress(loaded, billedLines, received: false);

                if (loaded.Items.Count > 0 && loaded.Items.All(i => i.BilledQuantity >= i.Quantity))
                {
                    loaded.Status = PurchaseOrderStatus.Completed;
                }

                await _purchases.UpdateOrderAsync(loaded, token);
            }

            // Extensibility Fase 1: After hooks inside the same transaction (no half-states).
            await DocumentLifecycleInvoker.RunAfterSubmitAsync(_invoiceHooks, invoice, token);

            return BuildInvoiceResult(invoice, glLines, items, ordersToUpdate.FirstOrDefault()?.Status);
        }, cancellationToken);
    }

    // -------------------------------------------------------------------- return (module 19 / T1)

    public async Task<PurchaseInvoicePostingDto> PostReturnAsync(
        PurchaseReturnPostingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        PurchaseValidator.EnsureHasLines(request.Lines);
        PurchaseValidator.EnsureValidTaxAmount(request.TaxAmount);

        // One transaction for the WHOLE posting: validation, SLE, GL, voucher, save and the
        // original-bill amortization commit together or not at all.
        return await _purchases.ExecuteInTransactionAsync(async token =>
        {
            var company = await _companies.GetByIdAsync(request.CompanyId, token)
                ?? throw new StockValidationException(
                    StockErrorCodes.CompanyNotFound,
                    $"Company '{request.CompanyId}' was not found in this tenant.");

            // Hard fiscal lock FIRST (same rationale as the receipt/invoice postings): a back-dated
            // return modifies zero data.
            company.EnsurePostingDateUnlocked(request.PostingDate);
            await _companies.EnsurePostingDateInOpenYearAsync(company.Id, request.PostingDate, token);

            var original = await _purchases.GetInvoiceByIdAsync(request.ReturnAgainstId, token);
            if (original is null || original.CompanyId != company.Id)
            {
                throw new PurchaseValidationException(
                    PurchaseErrorCodes.ReturnAgainstRequired,
                    $"Purchase bill '{request.ReturnAgainstId}' was not found in company '{company.Id}'.");
            }

            // Provenance first (exists, not a return, not cancelled): a return-of-a-return must fail
            // with its own code, before any per-line quantity comparison.
            PurchaseValidator.EnsureReturnTarget(original);

            var originalLinesById = original.Lines.ToDictionary(l => l.Id);

            // Resolve the requested lines against the ORIGINAL bill and total the net (foreign).
            var planned = new List<ReturnLinePlan>(request.Lines.Count);
            var netFc = 0m;
            for (var index = 0; index < request.Lines.Count; index++)
            {
                var line = request.Lines[index];
                PurchaseValidator.EnsureValidReturnLine(line.Qty);

                if (!originalLinesById.TryGetValue(line.PurchaseInvoiceLineId, out var originalLine))
                {
                    throw new PurchaseValidationException(
                        PurchaseErrorCodes.ReturnAgainstInvalid,
                        $"Return line references invoice line '{line.PurchaseInvoiceLineId}', which "
                        + $"does not belong to bill '{original.VoucherNo}'.");
                }

                // Per-line ceiling against the ORIGINAL billed quantity (the gross ceiling below
                // bounds the SUM across all debit notes).
                if (line.Qty > originalLine.Qty)
                {
                    throw new PurchaseValidationException(
                        PurchaseErrorCodes.ReturnAmountExceeded,
                        $"Return of {line.Qty:0.####} exceeds the billed quantity "
                        + $"{originalLine.Qty:0.####} of invoice line {originalLine.LineNumber}.");
                }

                var lineNetFc = Round4(line.Qty * originalLine.Rate);
                netFc += lineNetFc;
                planned.Add(new ReturnLinePlan(index + 1, originalLine, line.Qty, lineNetFc));
            }

            netFc = Round4(netFc);
            var taxFc = Round4(request.TaxAmount);
            var grossFc = Round4(netFc + taxFc);

            // Provenance + cumulative ceiling (scenario RET-G3): the return cannot exceed what the
            // original bill still has available after every debit note already booked against it.
            var alreadyReturned = await _purchases.GetReturnedAmountForInvoiceAsync(original.Id, token);
            PurchaseValidator.EnsureReturnAmountWithinCeiling(original, alreadyReturned, grossFc);

            // --- FX resolution (mirrors PostInvoiceAsync).
            var exchangeRate = 1m;
            if (request.CurrencyId.HasValue && company.CurrencyId.HasValue && request.CurrencyId != company.CurrencyId)
            {
                var rateRow = await _exchangeRates.GetLatestRateAsync(
                    request.CurrencyId.Value, company.CurrencyId.Value, request.PostingDate, token);
                if (rateRow == null)
                {
                    throw new PurchaseValidationException(
                        FxErrorCodes.ExchangeRateMissing, $"Missing exchange rate for {request.CurrencyId.Value}");
                }
                exchangeRate = rateRow.Rate;
            }

            var payableAccount = await RequireAccountByCodeAsync(
                company.Id, company.AccountsPayableAccountCode, "Company.AccountsPayableAccountCode", token);

            var items = await LoadItemsAsync(planned.Select(p => p.OriginalLine.ItemId), token);

            // Batch-and-serial Fase 3 boundary: a stock-moving return of tracked items has no
            // batch/serial return flow yet (which serials come back? to which batch?) - refuse
            // explicitly rather than corrupting traceability. Amount-only returns stay legal.
            if (request.UpdateStock)
            {
                foreach (var plan in planned)
                {
                    var plannedItem = items[plan.OriginalLine.ItemId];
                    if (plannedItem.HasBatchNo || plannedItem.HasSerialNo)
                    {
                        throw new StockValidationException(
                            StockErrorCodes.TrackedReturnUnsupported,
                            $"Item '{plannedItem.ItemCode}' is batch/serial-tracked: stock-moving returns "
                            + "of tracked items are not supported yet (post an amount-only return).");
                    }
                }
            }

            var glLines = new List<GLEntry>(4);
            var ledger = new List<StockLedgerEntry>();

            if (request.UpdateStock)
            {
                // Goods come back to the supplier: negative Kardex rows + a credit to the inventory
                // account of each returned line's warehouse. A bill may reference receipts from more
                // than one warehouse, so the credit is accumulated per stock account.
                var receiptLineIds = planned.Select(p => p.OriginalLine.PurchaseReceiptLineId).Distinct().ToList();
                var receiptLines = await _purchases.GetReceiptLinesByIdsAsync(receiptLineIds, token);
                var receiptLinesById = receiptLines.ToDictionary(l => l.Id);
                var creditsByAccount = new Dictionary<Guid, decimal>();
                var accountsById = new Dictionary<Guid, Account>();

                foreach (var plan in planned)
                {
                    if (!receiptLinesById.TryGetValue(plan.OriginalLine.PurchaseReceiptLineId, out var receiptLine)
                        || receiptLine.PurchaseReceipt is null)
                    {
                        throw new PurchaseValidationException(
                            PurchaseErrorCodes.PurchaseReceiptNotFound,
                            $"Receipt line '{plan.OriginalLine.PurchaseReceiptLineId}' backing invoice "
                            + $"line {plan.OriginalLine.LineNumber} was not found.");
                    }

                    var warehouse = await ResolveWarehouseAsync(
                        receiptLine.PurchaseReceipt.WarehouseId, company.Id, token);
                    var stockAccount = await RequirePostableAccountAsync(
                        warehouse.AccountId ?? Guid.Empty, company.Id, $"warehouse '{warehouse.WarehouseCode}'", token);

                    accountsById[stockAccount.Id] = stockAccount;
                    creditsByAccount[stockAccount.Id] = creditsByAccount.TryGetValue(stockAccount.Id, out var acc)
                        ? acc + plan.NetFc
                        : plan.NetFc;

                    // Goods leave: negative quantity/amount (the GL keeps positive magnitudes).
                    ledger.Add(new StockLedgerEntry
                    {
                        Id = Guid.NewGuid(),
                        ItemId = plan.OriginalLine.ItemId,
                        WarehouseId = warehouse.Id,
                        PostingDate = request.PostingDate,
                        QtyChange = -plan.Quantity,
                        ValuationRate = plan.OriginalLine.Rate,
                        Amount = -plan.NetFc,
                        CreatedAt = DateTimeOffset.UtcNow,
                    });
                }

                foreach (var (accountId, accountNetFc) in creditsByAccount)
                {
                    if (accountNetFc == 0m)
                    {
                        continue;
                    }

                    AddGlLine(
                        glLines, request.PostingDate, company.Id, InvoiceVoucherType, accountsById[accountId],
                        debit: 0m, credit: accountNetFc * exchangeRate,
                        creditInFC: accountNetFc,
                        remarks: "PurchaseReturn: inventory re-issue");
                }
            }
            else if (netFc != 0m)
            {
                // Amount-only adjustment (price correction): no physical movement, so the interim
                // accrued liability the original bill debited is reopened instead of touching stock.
                var receivedAccount = await RequireAccountByCodeAsync(
                    company.Id, company.StockReceivedAccountCode, "Company.StockReceivedAccountCode", token);

                AddGlLine(
                    glLines, request.PostingDate, company.Id, InvoiceVoucherType, receivedAccount,
                    debit: 0m, credit: netFc * exchangeRate,
                    creditInFC: netFc,
                    remarks: "PurchaseReturn: accrued liability reopened (no stock movement)");
            }

            if (taxFc > 0m)
            {
                var taxAccount = await RequireAccountByCodeAsync(
                    company.Id, company.InputTaxRecoverableAccountCode, "Company.InputTaxRecoverableAccountCode", token);

                AddGlLine(
                    glLines, request.PostingDate, company.Id, InvoiceVoucherType, taxAccount,
                    debit: 0m, credit: taxFc * exchangeRate,
                    creditInFC: taxFc,
                    remarks: "PurchaseReturn: input tax reversal");
            }

            // Gross payable reduction: Debit Accounts Payable (mirror of spec BY-01's credit). The
            // debit takes the SUM of the credits already booked so FX rounding can never unbalance
            // the voucher (the FC amounts stay exact: gross = net + tax).
            var payableDebitCompany = glLines.Sum(l => l.Credit);
            AddGlLine(
                glLines, request.PostingDate, company.Id, InvoiceVoucherType, payableAccount,
                debit: payableDebitCompany, credit: 0m,
                debitInFC: grossFc, creditInFC: 0m,
                remarks: "PurchaseReturn: accounts payable");

            // Constitution III.1: the voucher must balance to four decimals BEFORE anything is saved.
            DoubleEntryGuard.EnsureBalanced(glLines);

            var returnInvoice = new PurchaseInvoice
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                SupplierId = original.SupplierId,
                BillNumber = request.BillNumber,
                PostingDate = request.PostingDate,
                DueDate = request.DueDate,
                Status = PurchaseInvoiceStatus.Unpaid,
                CurrencyId = request.CurrencyId,
                ExchangeRate = exchangeRate,
                NetTotal = -netFc,
                TaxTotal = -taxFc,
                WithholdingTaxTotal = 0m,
                GrandTotal = -grossFc,
                OutstandingAmount = -grossFc,
                IsReturn = true,
                ReturnAgainstId = original.Id,
                UpdateStock = request.UpdateStock,
                VoucherNo = await _purchases.NextInvoiceVoucherNumberAsync(
                    company.Id, ReturnVoucherPrefix, request.PostingDate.Year, token),
                CreatedAt = DateTimeOffset.UtcNow,
                Lines = planned.Select(p => new PurchaseInvoiceLine
                {
                    Id = Guid.NewGuid(),
                    PurchaseReceiptLineId = p.OriginalLine.PurchaseReceiptLineId,
                    ItemId = p.OriginalLine.ItemId,
                    Qty = -p.Quantity,
                    Rate = p.OriginalLine.Rate,
                    Amount = -p.NetFc,
                    LineNumber = p.LineNumber,
                }).ToList(),
            };

            foreach (var entry in ledger)
            {
                entry.VoucherType = InvoiceVoucherType;
                entry.VoucherNo = returnInvoice.VoucherNo;
            }

            foreach (var glLine in glLines)
            {
                glLine.VoucherNo = returnInvoice.VoucherNo;
                glLine.VoucherId = returnInvoice.Id;
                glLine.PartyType = SupplierPartyType;
                glLine.PartyId = original.SupplierId;
            }

            // Extensibility Fase 1 (gap D3): same Before/After contract as PostInvoiceAsync -
            // a supplier debit note is a PurchaseInvoice submit as far as verticals care.
            await DocumentLifecycleInvoker.RunBeforeSubmitAsync(_invoiceHooks, returnInvoice, token);

            await _purchases.AddInvoiceAsync(returnInvoice, token);
            await _stock.AddGlEntriesAsync(glLines, token);
            if (ledger.Count > 0)
            {
                await _stock.AddLedgerEntriesAsync(ledger, token);
            }

            // Module 19 (gap D3): the debit note amortizes the referenced bill's outstanding
            // balance, floored at zero (mirrors the sales credit-note amortization).
            original.OutstandingAmount = Math.Max(0m, original.OutstandingAmount - grossFc);
            await _purchases.UpdateInvoiceAsync(original, token);

            await DocumentLifecycleInvoker.RunAfterSubmitAsync(_invoiceHooks, returnInvoice, token);

            return BuildInvoiceResult(returnInvoice, glLines, items, null);
        }, cancellationToken);
    }

    /// <summary>
    /// Tax-engine Fase 2 row plan: ordered engine inputs with their validated accounts, wire
    /// rates/fixed amounts for persistence, and per-row GL remarks. <c>Persist</c> is false
    /// for the legacy scalar path (zero rows stored, historical remark kept verbatim).
    /// </summary>
    private sealed record PurchaseTaxPlan(
        IReadOnlyList<TaxRowInput> Rows,
        IReadOnlyList<Account> Accounts,
        IReadOnlyList<decimal> Rates,
        IReadOnlyList<decimal> FixedAmounts,
        IReadOnlyList<string?> Remarks,
        bool Persist);

    /// <summary>
    /// Resolves the purchase tax rows: template expansion (template lines first, then manual
    /// rows) or the legacy scalar synthesized as ONE FixedAmount Add row against the
    /// input-tax account. Purchase-side accounts need only be active leaves of the company -
    /// input-tax assets, withholding liabilities and non-recoverable expense pass-throughs
    /// are all legitimate, so (unlike sales) no RootType is enforced.
    /// </summary>
    private async Task<PurchaseTaxPlan> ResolvePurchaseTaxRowsAsync(
        PurchaseInvoicePostingRequest request, Company company, CancellationToken token)
    {
        var companyId = company.Id;
        var rows = new List<TaxRowInput>();
        var accounts = new List<Account>();
        var rates = new List<decimal>();
        var fixedAmounts = new List<decimal>();
        var remarks = new List<string?>();

        if (request.TaxTemplateId is { } templateId && templateId != Guid.Empty)
        {
            if (_taxTemplates is null)
            {
                throw new TaxValidationException(
                    TaxErrorCodes.TaxTemplateNotFound,
                    $"Tax template '{templateId}' was not found in this tenant.");
            }

            var template = await _taxTemplates.GetByIdAsync(templateId, token)
                ?? throw new TaxValidationException(
                    TaxErrorCodes.TaxTemplateNotFound,
                    $"Tax template '{templateId}' was not found in this tenant.");

            if (template.CompanyId != companyId)
            {
                throw new TaxValidationException(
                    TaxErrorCodes.TaxTemplateNotFound,
                    $"Tax template '{template.Title}' does not belong to this company.");
            }

            if (!string.Equals(template.Category, "Purchase", StringComparison.OrdinalIgnoreCase))
            {
                throw new TaxValidationException(
                    TaxErrorCodes.InvalidTaxTemplateCategory,
                    $"Tax template '{template.Title}' is a '{template.Category}' template and cannot be applied to a purchase invoice.");
            }

            foreach (var line in template.Taxes)
            {
                if (line.AccountId == Guid.Empty)
                {
                    throw new TaxValidationException(
                        TaxErrorCodes.TaxAccountRequired,
                        $"Template '{template.Title}' line '{line.Description}' has no GL account assigned.");
                }

                accounts.Add(await RequirePurchaseTaxLeafAsync(line.AccountId, companyId, token));
                rates.Add(line.Rate);
                fixedAmounts.Add(line.Amount);
                remarks.Add(null);
                rows.Add(new TaxRowInput(line.Rate, line.ChargeType, line.CalculationBasis, line.Amount));
            }
        }

        foreach (var tax in request.Taxes ?? (IReadOnlyList<PurchaseInvoiceTaxInput>)Array.Empty<PurchaseInvoiceTaxInput>())
        {
            accounts.Add(await RequirePurchaseTaxLeafAsync(tax.AccountId, companyId, token));
            rates.Add(tax.Rate);
            fixedAmounts.Add(tax.FixedAmount);
            remarks.Add(null);
            rows.Add(new TaxRowInput(tax.Rate, tax.ChargeType, tax.CalculationBasis, tax.FixedAmount));
        }

        if (rows.Count == 0)
        {
            var legacyAmount = Round4(request.TaxAmount);
            if (legacyAmount == 0m)
            {
                return new PurchaseTaxPlan(rows, accounts, rates, fixedAmounts, remarks, Persist: false);
            }

            var taxAccount = await RequireAccountByCodeAsync(
                companyId,
                company.InputTaxRecoverableAccountCode,
                "Company.InputTaxRecoverableAccountCode",
                token);
            accounts.Add(taxAccount);
            rates.Add(0m);
            fixedAmounts.Add(legacyAmount);
            remarks.Add("PurchaseInvoice: input tax recoverable");
            rows.Add(new TaxRowInput(0m, TaxChargeType.Add, TaxCalculationBasis.FixedAmount, legacyAmount));
            return new PurchaseTaxPlan(rows, accounts, rates, fixedAmounts, remarks, Persist: false);
        }

        return new PurchaseTaxPlan(rows, accounts, rates, fixedAmounts, remarks, Persist: true);
    }

    /// <summary>Active-leaf gate for purchase tax rows (no RootType enforcement, see above).</summary>
    private async Task<Account> RequirePurchaseTaxLeafAsync(Guid accountId, Guid companyId, CancellationToken token)
    {
        var account = await _accounts.GetByIdAsync(accountId, token);
        if (account is null || account.CompanyId != companyId || !account.IsActive || account.IsGroup)
        {
            throw new TaxValidationException(
                TaxErrorCodes.TaxAccountRequired,
                $"Tax row account '{accountId}' must be an active posting (leaf) account of this company.");
        }

        return account;
    }

    /// <summary>Resolved plan of one return line (see <see cref="PostReturnAsync"/>).</summary>
    private sealed record ReturnLinePlan(
        int LineNumber,
        PurchaseInvoiceLine OriginalLine,
        decimal Quantity,
        decimal NetFc);

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

    // --------------------------------------------------- asset capitalization (module D1-D5)

    /// <summary>
    /// Resolves the capitalization plan of ONE fixed-asset invoice line BEFORE any write: the item's
    /// asset category, its postable cost account and the source warehouse's stock account the value
    /// is reclassified out of. Lines billed at a different rate than received are rejected so the
    /// stock account clears exactly (the reclass credits stock at the value the receipt debited).
    /// </summary>
    private async Task<AssetLinePlan> PlanAssetCapitalizationAsync(
        int index,
        PurchaseInvoicePostingLine line,
        PurchaseReceiptLine receiptLine,
        Item item,
        Guid companyId,
        decimal exchangeRate,
        CancellationToken token)
    {
        if (item.AssetCategoryId is not { } categoryId)
        {
            throw new AssetValidationException(
                AssetErrorCodes.AssetCategoryRequired,
                $"Item '{item.ItemCode}' is flagged as a fixed asset but has no asset category linked.");
        }

        var category = await _assets.GetCategoryByIdAsync(categoryId, token)
            ?? throw new AssetValidationException(
                AssetErrorCodes.CategoryNotFound,
                $"Asset category '{categoryId}' of item '{item.ItemCode}' was not found in this tenant.");

        if (category.CompanyId != companyId)
        {
            throw new AssetValidationException(
                AssetErrorCodes.CategoryNotFound,
                $"Asset category '{category.CategoryName}' does not belong to company '{companyId}'.");
        }

        if (!category.IsActive)
        {
            throw new AssetValidationException(
                AssetErrorCodes.InactiveCategory,
                $"Asset category '{category.CategoryName}' is inactive and cannot capitalize assets.");
        }

        if (category.FixedAssetAccountId == Guid.Empty
            || category.AccumulatedDepreciationAccountId == Guid.Empty
            || category.DepreciationExpenseAccountId == Guid.Empty)
        {
            throw new AssetValidationException(
                AssetErrorCodes.AssetCategoryMissingAccounts,
                $"Asset category '{category.CategoryName}' is missing its cost, accumulated-depreciation "
                + "or depreciation-expense account; configure the category before billing fixed assets.");
        }

        if (line.Rate != receiptLine.Rate)
        {
            throw new AssetValidationException(
                AssetErrorCodes.AssetLineRateMismatch,
                $"Fixed-asset line '{item.ItemCode}' must be billed at the received rate "
                + $"({receiptLine.Rate:0.######}); received {line.Rate:0.######}.");
        }

        var costAccount = await AssetAccountGuards.RequirePostableAccountAsync(
            _accounts, category.FixedAssetAccountId, companyId,
            $"asset category '{category.CategoryName}' cost account", token);

        var stockAccount = await ResolveReceiptStockAccountAsync(receiptLine, companyId, token);

        var billedFc = Round4(line.Qty * line.Rate);
        return new AssetLinePlan(
            index,
            item,
            category,
            line.Qty,
            billedFc,
            Round4(billedFc * exchangeRate),
            costAccount,
            stockAccount);
    }

    private async Task<Account> ResolveReceiptStockAccountAsync(
        PurchaseReceiptLine receiptLine,
        Guid companyId,
        CancellationToken token)
    {
        var receipt = receiptLine.PurchaseReceipt
            ?? throw new PurchaseValidationException(
                PurchaseErrorCodes.PurchaseReceiptNotFound,
                $"Receipt line '{receiptLine.Id}' is missing its receipt.");

        var warehouse = await _warehouses.GetByIdAsync(receipt.WarehouseId, token)
            ?? throw new PurchaseValidationException(
                PurchaseErrorCodes.PurchaseReceiptNotFound,
                $"Warehouse '{receipt.WarehouseId}' of receipt line '{receiptLine.Id}' was not found.");

        return await RequirePostableAccountAsync(
            warehouse.AccountId ?? Guid.Empty, companyId, $"warehouse '{warehouse.WarehouseCode}'", token);
    }

    private async Task CreateAssetFromInvoiceLineAsync(
        AssetLinePlan plan,
        PurchaseInvoice invoice,
        PurchaseInvoiceLine invoiceLine,
        DateOnly postingDate,
        Guid companyId,
        CancellationToken token)
    {
        var gross = plan.BilledCompany;
        var salvage = Round4(gross * plan.Category.DefaultSalvageValuePercent / 100m);

        var asset = Asset.CreateFromPurchase(
            companyId,
            plan.Item.Id,
            plan.Category.Id,
            plan.Item.ItemName,
            gross,
            salvage,
            plan.Category.DefaultUsefulLifeMonths,
            postingDate,
            postingDate,
            invoice.Id,
            invoiceLine.Id);

        asset.AssetCode = await _assets.NextAssetCodeAsync(companyId, postingDate.Year, token);

        var scheduleRows = new List<AssetDepreciationSchedule>();
        if (!plan.Category.IsNonDepreciable)
        {
            var planned = DepreciationScheduler.GenerateStraightLineSchedule(
                gross,
                salvage,
                asset.TotalNumberOfDepreciations,
                asset.FrequencyInMonths,
                asset.AvailableForUseDate);

            foreach (var row in planned)
            {
                scheduleRows.Add(new AssetDepreciationSchedule
                {
                    Id = Guid.NewGuid(),
                    AssetId = asset.Id,
                    ScheduleDate = row.ScheduleDate,
                    DepreciationAmount = row.DepreciationAmount,
                    AccumulatedDepreciationAfter = row.AccumulatedDepreciation,
                    Status = AssetScheduleStatus.Scheduled,
                });
            }
        }

        asset.Capitalize();

        await _assets.AddAssetAsync(asset, token);
        if (scheduleRows.Count > 0)
        {
            await _assets.AddScheduleRangeAsync(scheduleRows, token);
        }
    }

    /// <summary>Resolved capitalization plan of one fixed-asset invoice line (see PlanAssetCapitalizationAsync).</summary>
    private sealed record AssetLinePlan(
        int Index,
        Item Item,
        AssetCategory Category,
        decimal Quantity,
        decimal BilledFc,
        decimal BilledCompany,
        Account CostAccount,
        Account StockAccount);

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

    // ------------------------------------------------------------------------- order progress

    /// <summary>
    /// Task 4.5 acceptance (verify W5): mirrors posted quantities onto the linked order's lines
    /// (matched by ItemId, greedily filling the lines that still have headroom) and refreshes both
    /// header percentages from the line totals. Items the order does not buy are ignored - a
    /// receipt may reference an order while posting an item outside it, which is not progress.
    /// </summary>
    private static void ApplyOrderProgress(
        PurchaseOrder order,
        IEnumerable<(Guid ItemId, decimal Qty)> postedLines,
        bool received)
    {
        foreach (var (itemId, qty) in postedLines)
        {
            var targets = order.Items.Where(i => i.ItemId == itemId).ToList();
            if (targets.Count == 0)
            {
                continue;
            }

            var target = (received
                    ? targets.FirstOrDefault(i => i.ReceivedQuantity < i.Quantity)
                    : targets.FirstOrDefault(i => i.BilledQuantity < i.Quantity))
                ?? targets[0];

            if (received)
            {
                target.ReceivedQuantity += qty;
            }
            else
            {
                target.BilledQuantity += qty;
            }
        }

        var orderedQty = order.Items.Sum(i => i.Quantity);
        if (orderedQty <= 0m)
        {
            return;
        }

        // decimal(5,2) columns: round half away from zero, same convention as Round4.
        order.ReceivedPercentage = Math.Round(
            order.Items.Sum(i => i.ReceivedQuantity) / orderedQty * 100m,
            2,
            MidpointRounding.AwayFromZero);
        order.BilledPercentage = Math.Round(
            order.Items.Sum(i => i.BilledQuantity) / orderedQty * 100m,
            2,
            MidpointRounding.AwayFromZero);
    }

    // ------------------------------------------------------------------------------- persistence

    private static StockLedgerEntry NewLedgerEntry(
        PurchaseReceiptPostingLine line,
        Guid warehouseId,
        PurchaseReceiptPostingRequest request,
        decimal qtyChange,
        decimal valuationRate,
        decimal amount,
        Guid? batchId = null) =>
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
        string remarks,
        decimal? debitInFC = null,
        decimal? creditInFC = null) =>
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

            // plan.md §2 account-currency pair: single-currency postings book the ledger amount
            // 1:1 and snapshot the account currency (multi-currency restatement = spec AC-05, later).
            DebitInAccountCurrency = debitInFC ?? Round4(debit),
            CreditInAccountCurrency = creditInFC ?? Round4(credit),
            AccountCurrency = account.Currency?.Code ?? "USD",

            VoucherType = voucherType,

            // VoucherNo + VoucherId are stamped after the gapless number is assigned and the
            // source aggregate exists (same transaction); the supplier party (when the document
            // chain reaches a PurchaseOrder) and the absent cost center are stamped there too.
            VoucherNo = string.Empty,
            VoucherId = Guid.Empty,
            PartyType = null,
            PartyId = null,
            CostCenterId = null,
            IsCancelled = false,
            Remarks = remarks,
        });

    private static List<PurchaseReceiptLine> BuildReceiptLines(
        PurchaseReceiptPostingRequest request,
        IReadOnlyDictionary<Guid, Item> items,
        IReadOnlyDictionary<int, Guid?> batchByLine)
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
                BatchId = batchByLine.TryGetValue(i, out var batchId) ? batchId : null,
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
                Amount = Round4(line.Qty * line.Rate),
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
            .Select(e => new StockLedgerEntryDto(e.Id, e.ItemId, e.WarehouseId, e.PostingDate, e.QtyChange, e.ValuationRate, e.Amount, e.BatchId))
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

