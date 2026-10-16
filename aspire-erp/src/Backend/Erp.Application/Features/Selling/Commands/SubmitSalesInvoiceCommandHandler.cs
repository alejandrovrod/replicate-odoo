using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using Erp.Domain.Services;

namespace Erp.Application.Features.Selling.Commands;

public sealed class SubmitSalesInvoiceCommandHandler : ICommandHandler<SubmitSalesInvoiceCommand, Result<SalesInvoiceDto>>
{
    private readonly ISalesInvoiceRepository _salesInvoices;
    private readonly ICustomerRepository _customers;
    private readonly ICompanyRepository _companies;
    private readonly IAccountRepository _accounts;
    private readonly IExchangeRateRepository _exchangeRates;
    private readonly IStockRepository _stock;
    private readonly IWarehouseRepository _warehouses;
    private readonly IItemRepository? _items;
    private readonly IBankRepository? _banks;
    private readonly IEnumerable<IDocumentLifecycleHook<SalesInvoice>>? _hooks;

    public SubmitSalesInvoiceCommandHandler(
        ISalesInvoiceRepository salesInvoices,
        ICustomerRepository customers,
        ICompanyRepository companies,
        IAccountRepository accounts,
        IExchangeRateRepository exchangeRates,
        IStockRepository stock,
        IWarehouseRepository warehouses,
        IEnumerable<IDocumentLifecycleHook<SalesInvoice>>? hooks = null,
        IItemRepository? items = null,
        IBankRepository? banks = null)
    {
        _salesInvoices = salesInvoices;
        _customers = customers;
        _companies = companies;
        _accounts = accounts;
        _exchangeRates = exchangeRates;
        _stock = stock;
        _warehouses = warehouses;
        _items = items;
        _banks = banks;
        _hooks = hooks;
    }

    public async Task<Result<SalesInvoiceDto>> HandleAsync(SubmitSalesInvoiceCommand request, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _salesInvoices.ExecuteInTransactionAsync(async token =>
            {
                var invoice = await _salesInvoices.GetByIdAsync(request.SalesInvoiceId, token);
                if (invoice is null) return Result<SalesInvoiceDto>.Failure(SellingErrorCodes.ValidationFailed, "Invoice not found.");

                if (invoice.CompanyId != request.CompanyId)
                    return Result<SalesInvoiceDto>.Failure(SellingErrorCodes.ValidationFailed, "Invoice company mismatch.");

                if (invoice.Status != SalesInvoiceStatus.Draft)
                    return Result<SalesInvoiceDto>.Failure(SellingErrorCodes.ValidationFailed, "Invoice must be Draft to submit.");

                var company = await _companies.GetByIdAsync(request.CompanyId, token);
                if (company is null) return Result<SalesInvoiceDto>.Failure(SellingErrorCodes.CompanyNotFound, "Company not found.");

                company.EnsurePostingDateUnlocked(invoice.PostingDate);
                // R-13 FC-04: closed fiscal year rejects the posting too (second half of plan.md §3).
                await _companies.EnsurePostingDateInOpenYearAsync(company.Id, invoice.PostingDate, token);

                // Extensibility Fase 1 (gap D3): vertical Before hooks run after the Core gates
                // and BEFORE any write (customer balance, invoice status, GL). A rejection aborts
                // inside the transaction, so zero ledger rows exist (EXT-G3). The hook's own
                // stable code (e.g. "insurance_authorization_required") travels verbatim via
                // LifecycleHookRejectedException, mapped by the catch below.
                await DocumentLifecycleInvoker.RunBeforeSubmitAsync(_hooks, invoice, token);

                var customer = await _customers.GetByIdAsync(invoice.CustomerId, token);
                if (customer is null) return Result<SalesInvoiceDto>.Failure(SellingErrorCodes.CustomerNotFound, "Customer not found.");

                // ERPNext parity (advances allocation): the crossed portion is already funded,
                // so neither the credit exposure nor the customer balance counts it. Drafts
                // without advances keep netOutstanding == GrandTotal (behavior unchanged).
                var netOutstanding = invoice.GrandTotal - invoice.TotalAdvance;

                // spec SL-02 / plan.md §2: credit gate. Returns carry a negative GrandTotal, so
                // exposure can only shrink - the evaluator passes them without special-casing.
                CreditControlEvaluator.ValidateCreditExposure(customer, netOutstanding);

                // Update outstanding amount on customer (a return reduces the debt by construction).
                customer.OutstandingAmount += netOutstanding;
                await _customers.UpdateAsync(customer, token);
                // FX Resolution
                if (invoice.CurrencyId.HasValue && company.CurrencyId.HasValue && invoice.CurrencyId != company.CurrencyId)
                {
                    var rateRow = await _exchangeRates.GetLatestRateAsync(invoice.CurrencyId.Value, company.CurrencyId.Value, invoice.PostingDate, token);
                    if (rateRow == null)
                        return Result<SalesInvoiceDto>.Failure(FxErrorCodes.ExchangeRateMissing, $"Missing rate for {invoice.CurrencyId}");
                    invoice.ExchangeRate = rateRow.Rate;
                }
                else
                {
                    invoice.ExchangeRate = 1m;
                }

                invoice.Status = invoice.TotalAdvance <= 0m
                    ? SalesInvoiceStatus.Unpaid
                    : netOutstanding <= 0m
                        ? SalesInvoiceStatus.Paid
                        : SalesInvoiceStatus.PartiallyPaid;
                invoice.OutstandingAmount = netOutstanding; // Just to be sure it matches
                invoice.PaidAmount = invoice.TotalAdvance;
                await _salesInvoices.UpdateAsync(invoice, token);

                var glEntries = new List<GLEntry>();

                // Resolve the A/R + revenue anchors (Constitution III.3: exactly one active leaf).
                Guid receivableAccountId = await ResolveReceivableAccountAsync(customer, company, request.CompanyId, token);
                Guid incomeAccountId = await ResolveIncomeAccountAsync(company, request.CompanyId, token);

                // Module 17 + tax-engine Fase 2: every tax row re-resolves its account at the
                // money gate - creation-time checks do not survive chart edits between Draft
                // and submit. Add rows require a Liability leaf; Deduct rows (suffered
                // retentions, booked as debit) accept Liability or Asset leaves.
                var taxAccounts = new Dictionary<Guid, Account>();
                foreach (var tax in invoice.Taxes)
                {
                    taxAccounts[tax.Id] = await RequireTaxAccountAsync(tax.AccountId, company.Id, tax.ChargeType, token);
                }

                if (!invoice.IsReturn)
                {
                    // Standard sale: Debit A/R (Grand) / Credit revenue (Net) / per-row tax sides.
                    AddMoneyLine(glEntries, invoice, company, customer,
                        receivableAccountId, debit: invoice.GrandTotal, credit: 0m, "Sales Invoice Submission - A/R");
                    AddMoneyLine(glEntries, invoice, company, customer,
                        incomeAccountId, debit: 0m, credit: invoice.NetTotal, "Sales Invoice Submission - Revenue");

                    // Tax-engine Fase 2: sides follow the SIGNED row amount (Add rows are
                    // positive here → Credit; Deduct rows are negative → Debit, e.g. suffered
                    // retentions booked as "retención a favor"). Add-only invoices behave
                    // exactly as module 17 (Credit tax.TaxAmount).
                    foreach (var tax in invoice.Taxes)
                    {
                        var side = tax.TaxAmount >= 0
                            ? (debit: 0m, credit: tax.TaxAmount)
                            : (debit: -tax.TaxAmount, credit: 0m);
                        AddMoneyLine(glEntries, invoice, company, customer,
                            tax.AccountId, debit: side.debit, credit: side.credit,
                            $"Sales Invoice Submission - Tax {tax.Rate:0.##}% ({tax.ChargeType})");
                    }
                }
                else
                {
                    // Module 18 (ERPNext reversal parity) + tax-engine Fase 2: return rows
                    // already carry MIRRORED signs (negative base flips Add to negative and
                    // Deduct to positive), so the SAME sign rule books the exact inverse sides
                    // automatically - GLEntry forbids negative Debit/Credit (Constitution IV.3).
                    // Credit A/R / Debit revenue / sign-ruled tax sides.
                    var absGrand = Math.Abs(invoice.GrandTotal);
                    var absNet = Math.Abs(invoice.NetTotal);

                    AddMoneyLine(glEntries, invoice, company, customer,
                        receivableAccountId, debit: 0m, credit: absGrand, "Credit Note - A/R reversal");
                    AddMoneyLine(glEntries, invoice, company, customer,
                        incomeAccountId, debit: absNet, credit: 0m, "Credit Note - Revenue reversal");

                    foreach (var tax in invoice.Taxes)
                    {
                        var side = tax.TaxAmount >= 0
                            ? (debit: 0m, credit: tax.TaxAmount)
                            : (debit: -tax.TaxAmount, credit: 0m);
                        AddMoneyLine(glEntries, invoice, company, customer,
                            tax.AccountId, debit: side.debit, credit: side.credit,
                            $"Credit Note - Tax reversal {tax.Rate:0.##}% ({tax.ChargeType})");
                    }
                }

                // Constitution III.1: the voucher must balance to 0.0000 BEFORE anything is saved.
                // FX multiplication rounds each line to 4 decimals, so absorb the residual into
                // the revenue line (the interior anchor present in both directions).
                AbsorbRoundingResidual(glEntries, incomeAccountId, !invoice.IsReturn);
                DoubleEntryGuard.EnsureBalanced(glEntries);

                // Module 18 (gap D3 / scenario RET-G1): a credit note amortizes the ORIGINAL
                // invoice's outstanding balance. The customer's own balance already shrank above
                // (negative GrandTotal); this reduces the specific bill the note references, so
                // the referenced invoice stops showing as fully collectible.
                if (invoice.IsReturn)
                {
                    await AmortizeReturnedInvoiceAsync(invoice, token);
                }

                // Module 18 §3: stock re-entry on returns (and symmetric outflow on normal sales)
                // when the invoice moves inventory.
                if (invoice.UpdateStock)
                {
                    await PostStockAsync(invoice, company, customer, glEntries, token);
                    DoubleEntryGuard.EnsureBalanced(glEntries);
                }

                // ERPNext parity (advances allocation, SI-G2): consume the crossed slices from
                // the referenced payments' unallocated balances. No compensation GL is booked:
                // the payment already credited Receivable in full at submit, so the cross nets
                // inside A/R by construction (Dr A/R invoice leg vs the payment's Cr A/R leg).
                // Booking Dr Advances / Cr Receivable here would double-count the credit.
                if (invoice.TotalAdvance > 0m)
                {
                    await SettleAdvancesAsync(invoice, company, token);
                }

                invoice.Customer = customer;
                await _salesInvoices.AddGlEntriesAsync(glEntries, token);

                // Extensibility Fase 1: After hooks run inside the same transaction, after the
                // ledger rows are staged - a failure rolls everything back (no half-states).
                await DocumentLifecycleInvoker.RunAfterSubmitAsync(_hooks, invoice, token);

                return Result<SalesInvoiceDto>.Success(SalesInvoiceDto.Build(invoice));

            }, cancellationToken);
        }
        catch (CreditLimitExceededException ex)
        {
            return Result<SalesInvoiceDto>.Failure(ex.Code, ex.Message);
        }
        catch (LifecycleHookRejectedException ex)
        {
            // Extensibility Fase 1 (EXT-G3): the hook's own stable code travels verbatim
            // (e.g. "insurance_authorization_required"); the generic fallback is
            // "lifecycle_hook_rejected". The transaction rolled back: no GL was posted.
            return Result<SalesInvoiceDto>.Failure(ex.Code, ex.Message);
        }
        catch (WorkflowValidationException ex)
        {
            // Workflow Fase 5: the guard's own stable code travels verbatim
            // (workflow_submit_not_allowed); the invoice stays Draft with zero writes.
            return Result<SalesInvoiceDto>.Failure(ex.Code, ex.Message);
        }
        catch (SalesValidationException ex)
        {
            return Result<SalesInvoiceDto>.Failure(ex.Code, ex.Message);
        }
        catch (StockValidationException ex)
        {
            return Result<SalesInvoiceDto>.Failure(ex.Code, ex.Message);
        }
        catch (InsufficientStockException ex)
        {
            // FIFO shortfall on the UpdateStock leg (spec scenario SI-G1 gate): the request
            // is well-formed but the warehouse cannot cover it - 422, never a silent
            // negative post when the company forbids negative stock.
            return Result<SalesInvoiceDto>.Failure(
                SellingErrorCodes.InsufficientStockForInvoice, ex.Message);
        }
        catch (Exception ex)
        {
            return Result<SalesInvoiceDto>.Failure("server_error", ex.Message);
        }
    }

    // ------------------------------------------------------------------ account resolution

    /// <summary>
    /// Module 18 (gap D3): reduces the referenced invoice's <see cref="SalesInvoice.OutstandingAmount"/>
    /// by the credit note's absolute grand total, floored at zero so rounding overlap cannot drive it
    /// negative. The creation-time guard already proved the original exists, belongs to the same
    /// customer and is submitted, so a missing row here is a no-op rather than a failure.
    /// </summary>
    private async Task AmortizeReturnedInvoiceAsync(SalesInvoice creditNote, CancellationToken token)
    {
        if (creditNote.ReturnAgainstId is not { } originalId || originalId == Guid.Empty)
        {
            return;
        }

        var original = await _salesInvoices.GetByIdAsync(originalId, token);
        if (original is null || original.IsReturn)
        {
            return;
        }

        var credit = Math.Abs(creditNote.GrandTotal);
        original.OutstandingAmount = Math.Max(0m, original.OutstandingAmount - credit);
        await _salesInvoices.UpdateAsync(original, token);
    }

    private async Task<Guid> ResolveReceivableAccountAsync(
        Customer customer, Company company, Guid companyId, CancellationToken token)
    {
        if (customer.DefaultReceivableAccountId.HasValue)
        {
            return customer.DefaultReceivableAccountId.Value;
        }

        if (!string.IsNullOrEmpty(company.DefaultReceivableAccountCode))
        {
            var accts = await _accounts.FindActiveLeafByCodeAsync(companyId, company.DefaultReceivableAccountCode, token);
            if (accts.Count == 1) return accts[0].Id;
        }

        return Guid.Empty;
    }

    private async Task<Guid> ResolveIncomeAccountAsync(Company company, Guid companyId, CancellationToken token)
    {
        if (!string.IsNullOrEmpty(company.DefaultIncomeAccountCode))
        {
            var accts = await _accounts.FindActiveLeafByCodeAsync(companyId, company.DefaultIncomeAccountCode, token);
            if (accts.Count == 1) return accts[0].Id;
        }

        return Guid.Empty;
    }

    /// <summary>
    /// Submit-time tax account gate (module 17 task item + tax-engine Fase 2): postable leaf
    /// of the company. Mirrors the creation-time rule so chart edits cannot smuggle revenue
    /// postings into the tax leg between Draft and submit: Add rows require Liability,
    /// Deduct rows accept Liability or Asset.
    /// </summary>
    private async Task<Account> RequireTaxAccountAsync(Guid accountId, Guid companyId, TaxChargeType chargeType, CancellationToken token)
    {
        var account = accountId == Guid.Empty
            ? null
            : await _accounts.GetByIdAsync(accountId, token);

        if (account is null || account.CompanyId != companyId)
        {
            throw new SalesValidationException(
                SellingErrorCodes.InvalidTaxAccount,
                $"Tax account '{accountId}' was not found in this company.");
        }

        if (!account.IsActive || account.IsGroup)
        {
            throw new SalesValidationException(
                SellingErrorCodes.InvalidTaxAccount,
                $"Tax account '{account.AccountCode}' must be an active leaf (posting) account.");
        }

        var allowed = chargeType == TaxChargeType.Deduct
            ? account.RootType is AccountRootType.Liability or AccountRootType.Asset
            : account.RootType == AccountRootType.Liability;
        if (!allowed)
        {
            throw new SalesValidationException(
                SellingErrorCodes.InvalidTaxAccount,
                chargeType == TaxChargeType.Deduct
                    ? $"Retention account '{account.AccountCode}' must be a Liability or Asset account, not '{account.RootType}'."
                    : $"Tax account '{account.AccountCode}' must be a Liability account, not '{account.RootType}'.");
        }

        return account;
    }

    // ---------------------------------------------------------------------- money GL lines

    /// <summary>
    /// Books one money leg in BOTH currencies, rounded to 4 decimals (decimal(18,4) columns).
    /// Amounts arrive non-negative by construction (returns pre-absolutize their magnitudes).
    /// </summary>
    private static void AddMoneyLine(
        List<GLEntry> glEntries,
        SalesInvoice invoice,
        Company company,
        Customer customer,
        Guid accountId,
        decimal debit,
        decimal credit,
        string remarks)
    {
        glEntries.Add(new GLEntry
        {
            TenantId = company.TenantId,
            CompanyId = company.Id,
            PostingDate = invoice.PostingDate,
            AccountId = accountId,
            Debit = Round4(debit * invoice.ExchangeRate),
            Credit = Round4(credit * invoice.ExchangeRate),
            DebitInAccountCurrency = Round4(debit),
            CreditInAccountCurrency = Round4(credit),
            AccountCurrency = invoice.Currency?.Code ?? "USD",
            VoucherType = "Sales Invoice",
            VoucherNo = invoice.InvoiceNumber,
            VoucherId = invoice.Id,
            PartyType = "Customer",
            PartyId = customer.Id,
            Remarks = remarks,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    /// <summary>
    /// FX rounding can leave Dr != Cr by 0.0001: push the residual into the revenue leg so the
    /// voucher balances exactly. <paramref name="revenueIsCredit"/> selects the normal (credit
    /// revenue) vs return (debit revenue) direction.
    /// </summary>
    private static void AbsorbRoundingResidual(List<GLEntry> glEntries, Guid incomeAccountId, bool revenueIsCredit)
    {
        var totalDebit = glEntries.Sum(l => l.Debit);
        var totalCredit = glEntries.Sum(l => l.Credit);
        var residual = Round4(totalDebit - totalCredit);

        if (residual == 0m)
        {
            return;
        }

        var revenue = glEntries.First(l => l.AccountId == incomeAccountId);
        if (revenueIsCredit)
        {
            revenue.Credit = Round4(revenue.Credit + residual);
            revenue.CreditInAccountCurrency = Round4(revenue.CreditInAccountCurrency + residual);
        }
        else
        {
            revenue.Debit = Round4(revenue.Debit - residual);
            revenue.DebitInAccountCurrency = Round4(revenue.DebitInAccountCurrency - residual);
        }
    }

    // ----------------------------------------------------------------------------- stock

    /// <summary>
    /// Inventory leg for UpdateStock invoices (sales-invoice ERPNext parity, spec scenario
    /// SI-G1): normal sales consume FIFO layers (Dr COGS at the FIFO cost / Cr Stock, negative
    /// SLE) exactly like the delivery-note engine; returns re-enter at the credit-note line
    /// rate (Dr Stock / Cr COGS, positive SLE - module 18 §3, unchanged).
    /// </summary>
    private async Task PostStockAsync(
        SalesInvoice invoice,
        Company company,
        Customer customer,
        List<GLEntry> glEntries,
        CancellationToken token)
    {
        if (!invoice.SourceWarehouseId.HasValue || invoice.SourceWarehouseId.Value == Guid.Empty)
        {
            throw new SalesValidationException(
                SellingErrorCodes.UpdateStockWarehouseRequired,
                "Invoices with UpdateStock must carry the source warehouse (SourceWarehouseId is required).");
        }

        var warehouse = await _warehouses.GetByIdAsync(invoice.SourceWarehouseId.Value, token);
        if (warehouse is null || warehouse.CompanyId != company.Id)
        {
            throw new StockValidationException(
                StockErrorCodes.WarehouseNotFound,
                $"Warehouse '{invoice.SourceWarehouseId}' was not found in this company.");
        }

        if (warehouse.AccountId is null || warehouse.AccountId.Value == Guid.Empty)
        {
            throw new StockValidationException(
                StockErrorCodes.InvalidGlAccount,
                $"Warehouse '{warehouse.WarehouseCode}' has no stock account linked.");
        }

        var stockAccount = await _accounts.GetByIdAsync(warehouse.AccountId.Value, token);
        if (stockAccount is null || !stockAccount.IsActive || stockAccount.IsGroup || stockAccount.CompanyId != company.Id)
        {
            throw new StockValidationException(
                StockErrorCodes.InvalidGlAccount,
                $"Warehouse '{warehouse.WarehouseCode}' stock account is not a postable leaf of this company.");
        }

        var cogsAccount = await RequireCogsAccountAsync(company, token);
        var ledger = new List<StockLedgerEntry>(invoice.Items.Count);

        // Task 3.9 overselling prevention: lock the Kardex ranges BEFORE reading layers so
        // two concurrent issues of the same (item, warehouse) pairs serialize on the lock
        // instead of consuming the same units (same pattern as StockPostingService).
        var itemIds = invoice.Items.Select(l => l.ItemId).Distinct().ToList();
        await _stock.LockStockRangeAsync(itemIds, new[] { warehouse.Id }, token);

        foreach (var line in invoice.Items)
        {
            // Returns arrive with negative quantities: direction follows the SIGN, magnitudes
            // stay positive for the GL (Constitution IV.3).
            var magnitude = Math.Abs(line.Quantity);
            var inflow = invoice.IsReturn;

            if (inflow)
            {
                // Re-entry: Debit Inventory / Credit COGS at the credit-note line rate
                // (module 18 §3 - the return has no FIFO cost of its own).
                var lineCost = Round4(magnitude * line.Rate);

                ledger.Add(new StockLedgerEntry
                {
                    Id = Guid.NewGuid(),
                    TenantId = company.TenantId,
                    ItemId = line.ItemId,
                    WarehouseId = warehouse.Id,
                    VoucherType = "Sales Invoice",
                    VoucherNo = invoice.InvoiceNumber,
                    PostingDate = invoice.PostingDate,
                    QtyChange = magnitude,
                    ValuationRate = line.Rate,
                    Amount = lineCost,
                    CreatedAt = DateTimeOffset.UtcNow,
                });

                AddStockGlLine(glEntries, invoice, company, customer.Id,
                    stockAccount.Id, debit: lineCost, credit: 0m, "Credit Note - Stock re-entry");
                AddStockGlLine(glEntries, invoice, company, customer.Id,
                    cogsAccount.Id, debit: 0m, credit: lineCost, "Credit Note - COGS reversal");
                continue;
            }

            // Outflow: FIFO consumption (shortfall throws InsufficientStockException unless
            // the company allows negative stock - mapped to insufficient_stock_for_invoice).
            var itemCode = (await ResolveItemCodeAsync(line.ItemId, token)) ?? line.ItemId.ToString();
            var persisted = await _stock.GetFifoLayersAsync(line.ItemId, warehouse.Id, invoice.PostingDate, token);
            var consumption = FifoValuation.Consume(
                FifoValuation.BuildLayers(persisted),
                magnitude,
                company.AllowNegativeStock,
                itemCode,
                warehouse.WarehouseCode);

            ledger.Add(new StockLedgerEntry
            {
                Id = Guid.NewGuid(),
                TenantId = company.TenantId,
                ItemId = line.ItemId,
                WarehouseId = warehouse.Id,
                VoucherType = "Sales Invoice",
                VoucherNo = invoice.InvoiceNumber,
                PostingDate = invoice.PostingDate,
                QtyChange = -magnitude,
                ValuationRate = consumption.AverageRate,
                Amount = -consumption.TotalCost,
                CreatedAt = DateTimeOffset.UtcNow,
            });

            // Issue: Debit COGS / Credit Stock at the FIFO cost (same pair as the
            // delivery-note engine - the billed rate only drives revenue, never COGS).
            AddStockGlLine(glEntries, invoice, company, customer.Id,
                cogsAccount.Id, debit: consumption.TotalCost, credit: 0m, "Sales Invoice - COGS");
            AddStockGlLine(glEntries, invoice, company, customer.Id,
                stockAccount.Id, debit: 0m, credit: consumption.TotalCost, "Sales Invoice - Stock issue");
        }

        await _stock.AddLedgerEntriesAsync(ledger, token);
    }

    private async Task<string?> ResolveItemCodeAsync(Guid itemId, CancellationToken token)
    {
        if (_items is null)
        {
            return null;
        }

        var item = await _items.GetByIdAsync(itemId, token);
        return item?.ItemCode;
    }

    /// <summary>
    /// Settles the Draft-time advance reservations against the live payment balances
    /// (spec scenario SI-G2): every slice re-checks company/party/state ownership and the
    /// CURRENT unallocated remainder (a concurrent cross lands here as
    /// <c>payment_entry_not_found_or_depleted</c>), then moves the amount from the
    /// payment's unallocated remainder into its allocated total under the PE-03
    /// conservation law. Runs inside the submit transaction: any failure rolls back the
    /// whole voucher with zero ledger rows.
    /// </summary>
    private async Task SettleAdvancesAsync(
        SalesInvoice invoice,
        Company company,
        CancellationToken token)
    {
        if (_banks is null)
        {
            throw new SalesValidationException(
                SellingErrorCodes.ValidationFailed,
                "Advance settlement is not wired for this caller.");
        }

        foreach (var slice in invoice.Advances)
        {
            var payment = await _banks.GetPaymentByIdAsync(slice.PaymentEntryId, token);
            if (payment is null
                || payment.CompanyId != company.Id
                || payment.PartyType != PaymentPartyType.Customer
                || payment.PartyId != invoice.CustomerId
                || payment.PaymentType != PaymentType.Receive
                || payment.DocumentStatus != PaymentDocumentStatus.Submitted)
            {
                throw new SalesValidationException(
                    SellingErrorCodes.PaymentEntryNotFoundOrDepleted,
                    $"Advance payment '{slice.PaymentEntryId}' was not found for this customer "
                    + "as a submitted receipt.");
            }

            if (slice.AllocatedAmount > payment.UnallocatedAmount)
            {
                throw new SalesValidationException(
                    SellingErrorCodes.PaymentEntryNotFoundOrDepleted,
                    $"Advance payment '{payment.VoucherNo}' only has "
                    + $"{payment.UnallocatedAmount:0.####} unallocated.");
            }

            payment.UnallocatedAmount -= slice.AllocatedAmount;
            payment.TotalAllocatedAmount += slice.AllocatedAmount;
            // The slice was born on the invoice side (Draft reservation); attaching the SAME
            // tracked instance to the payment side keeps PE-03 honest without a duplicate row
            // (guarded: a retried submit must not double-count the slice in memory).
            if (payment.Allocations.All(a => a.Id != slice.Id))
            {
                payment.Allocations.Add(slice);
            }
            payment.EnsureConservation(
                payment.Allocations.Select(a => a.AllocatedAmount),
                payment.UnallocatedAmount);

            slice.ExchangeRate = invoice.ExchangeRate;
            await _banks.UpdatePaymentAsync(payment, token);
        }
    }

    private async Task<Account> RequireCogsAccountAsync(Company company, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(company.CogsAccountCode))
        {
            throw new StockValidationException(
                StockErrorCodes.InvalidGlAccount,
                $"Company '{company.Id}' does not configure CogsAccountCode; seed it before posting stock.");
        }

        var matches = await _accounts.FindActiveLeafByCodeAsync(company.Id, company.CogsAccountCode, token);
        if (matches.Count != 1)
        {
            throw new StockValidationException(
                StockErrorCodes.InvalidGlAccount,
                $"CogsAccountCode = '{company.CogsAccountCode}' does not resolve to exactly one active leaf account.");
        }

        return matches[0];
    }

    private static void AddStockGlLine(
        List<GLEntry> glEntries,
        SalesInvoice invoice,
        Company company,
        Guid customerId,
        Guid accountId,
        decimal debit,
        decimal credit,
        string remarks)
    {
        glEntries.Add(new GLEntry
        {
            TenantId = company.TenantId,
            CompanyId = company.Id,
            PostingDate = invoice.PostingDate,
            AccountId = accountId,
            Debit = Round4(debit * invoice.ExchangeRate),
            Credit = Round4(credit * invoice.ExchangeRate),
            DebitInAccountCurrency = Round4(debit),
            CreditInAccountCurrency = Round4(credit),
            AccountCurrency = "USD",
            VoucherType = "Sales Invoice",
            VoucherNo = invoice.InvoiceNumber,
            VoucherId = invoice.Id,
            PartyType = "Customer",
            PartyId = customerId,
            Remarks = remarks,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    private static decimal Round4(decimal value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);
}
