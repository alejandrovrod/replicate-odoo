using Erp.Application.Common;
using Erp.Application.DTOs;
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

    public SubmitSalesInvoiceCommandHandler(
        ISalesInvoiceRepository salesInvoices,
        ICustomerRepository customers,
        ICompanyRepository companies,
        IAccountRepository accounts,
        IExchangeRateRepository exchangeRates,
        IStockRepository stock,
        IWarehouseRepository warehouses)
    {
        _salesInvoices = salesInvoices;
        _customers = customers;
        _companies = companies;
        _accounts = accounts;
        _exchangeRates = exchangeRates;
        _stock = stock;
        _warehouses = warehouses;
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

                var customer = await _customers.GetByIdAsync(invoice.CustomerId, token);
                if (customer is null) return Result<SalesInvoiceDto>.Failure(SellingErrorCodes.CustomerNotFound, "Customer not found.");

                // spec SL-02 / plan.md §2: credit gate. Returns carry a negative GrandTotal, so
                // exposure can only shrink - the evaluator passes them without special-casing.
                CreditControlEvaluator.ValidateCreditExposure(customer, invoice.GrandTotal);

                // Update outstanding amount on customer (a return reduces the debt by construction).
                customer.OutstandingAmount += invoice.GrandTotal;
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

                invoice.Status = SalesInvoiceStatus.Unpaid;
                invoice.OutstandingAmount = invoice.GrandTotal; // Just to be sure it matches
                await _salesInvoices.UpdateAsync(invoice, token);

                var glEntries = new List<GLEntry>();

                // Resolve the A/R + revenue anchors (Constitution III.3: exactly one active leaf).
                Guid receivableAccountId = await ResolveReceivableAccountAsync(customer, company, request.CompanyId, token);
                Guid incomeAccountId = await ResolveIncomeAccountAsync(company, request.CompanyId, token);

                // Module 17: every tax row re-resolves its liability account at the money gate -
                // creation-time checks do not survive chart edits between Draft and submit.
                var taxAccounts = new Dictionary<Guid, Account>();
                foreach (var tax in invoice.Taxes)
                {
                    taxAccounts[tax.Id] = await RequireTaxAccountAsync(tax.AccountId, company.Id, token);
                }

                if (!invoice.IsReturn)
                {
                    // Standard sale: Debit A/R (Grand) / Credit revenue (Net) / Credit taxes.
                    AddMoneyLine(glEntries, invoice, company, customer,
                        receivableAccountId, debit: invoice.GrandTotal, credit: 0m, "Sales Invoice Submission - A/R");
                    AddMoneyLine(glEntries, invoice, company, customer,
                        incomeAccountId, debit: 0m, credit: invoice.NetTotal, "Sales Invoice Submission - Revenue");

                    foreach (var tax in invoice.Taxes)
                    {
                        AddMoneyLine(glEntries, invoice, company, customer,
                            tax.AccountId, debit: 0m, credit: tax.TaxAmount,
                            $"Sales Invoice Submission - Tax {tax.Rate:0.##}%");
                    }
                }
                else
                {
                    // Module 18 (ERPNext reversal parity): explicit inverse sides with ABSOLUTE
                    // magnitudes - GLEntry forbids negative Debit/Credit (Constitution IV.3,
                    // CK_Debit/Credit_NonNegative), so the same engine can never just "multiply
                    // by -1". Credit A/R / Debit revenue / Debit taxes.
                    var absGrand = Math.Abs(invoice.GrandTotal);
                    var absNet = Math.Abs(invoice.NetTotal);

                    AddMoneyLine(glEntries, invoice, company, customer,
                        receivableAccountId, debit: 0m, credit: absGrand, "Credit Note - A/R reversal");
                    AddMoneyLine(glEntries, invoice, company, customer,
                        incomeAccountId, debit: absNet, credit: 0m, "Credit Note - Revenue reversal");

                    foreach (var tax in invoice.Taxes)
                    {
                        AddMoneyLine(glEntries, invoice, company, customer,
                            tax.AccountId, debit: Math.Abs(tax.TaxAmount), credit: 0m,
                            $"Credit Note - Tax reversal {tax.Rate:0.##}%");
                    }
                }

                // Constitution III.1: the voucher must balance to 0.0000 BEFORE anything is saved.
                // FX multiplication rounds each line to 4 decimals, so absorb the residual into
                // the revenue line (the interior anchor present in both directions).
                AbsorbRoundingResidual(glEntries, incomeAccountId, !invoice.IsReturn);
                DoubleEntryGuard.EnsureBalanced(glEntries);

                // Module 18 §3: stock re-entry on returns (and symmetric outflow on normal sales)
                // when the invoice moves inventory. Valuation follows the POS simplification
                // (line rate as valuation rate); the delivery-note FIFO engine stays untouched.
                if (invoice.UpdateStock)
                {
                    await PostStockAsync(invoice, company, customer, glEntries, token);
                    DoubleEntryGuard.EnsureBalanced(glEntries);
                }

                invoice.Customer = customer;
                await _salesInvoices.AddGlEntriesAsync(glEntries, token);

                return Result<SalesInvoiceDto>.Success(SalesInvoiceDto.Build(invoice));

            }, cancellationToken);
        }
        catch (CreditLimitExceededException ex)
        {
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
        catch (Exception ex)
        {
            return Result<SalesInvoiceDto>.Failure("server_error", ex.Message);
        }
    }

    // ------------------------------------------------------------------ account resolution

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
    /// Submit-time tax account gate (module 17 task item): postable LIABILITY leaf of the
    /// company. Mirrors the creation-time rule so chart edits cannot smuggle revenue postings
    /// into the tax leg between Draft and submit.
    /// </summary>
    private async Task<Account> RequireTaxAccountAsync(Guid accountId, Guid companyId, CancellationToken token)
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

        if (account.RootType != AccountRootType.Liability)
        {
            throw new SalesValidationException(
                SellingErrorCodes.InvalidTaxAccount,
                $"Tax account '{account.AccountCode}' must be a Liability account, not '{account.RootType}'.");
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
    /// Inventory leg for UpdateStock invoices (module 18 §3): normal sales consume
    /// (Dr COGS / Cr Stock, negative SLE), returns re-enter (Dr Stock / Cr COGS, positive
    /// SLE). Same FIFO-independent rate-as-valuation simplification as the POS path.
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
                SellingErrorCodes.ValidationFailed,
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

        foreach (var line in invoice.Items)
        {
            // Returns arrive with negative quantities: direction follows the SIGN, magnitudes
            // stay positive for the GL (Constitution IV.3).
            var magnitude = Math.Abs(line.Quantity);
            var lineCost = Round4(magnitude * line.Rate);
            var inflow = invoice.IsReturn;

            ledger.Add(new StockLedgerEntry
            {
                Id = Guid.NewGuid(),
                TenantId = company.TenantId,
                ItemId = line.ItemId,
                WarehouseId = warehouse.Id,
                VoucherType = "Sales Invoice",
                VoucherNo = invoice.InvoiceNumber,
                PostingDate = invoice.PostingDate,
                QtyChange = inflow ? magnitude : -magnitude,
                ValuationRate = line.Rate,
                Amount = inflow ? lineCost : -lineCost,
                CreatedAt = DateTimeOffset.UtcNow,
            });

            if (inflow)
            {
                // Re-entry: Debit Inventory / Credit COGS (spec 18 §3).
                AddStockGlLine(glEntries, invoice, company, customer.Id,
                    stockAccount.Id, debit: lineCost, credit: 0m, "Credit Note - Stock re-entry");
                AddStockGlLine(glEntries, invoice, company, customer.Id,
                    cogsAccount.Id, debit: 0m, credit: lineCost, "Credit Note - COGS reversal");
            }
            else
            {
                // Issue: Debit COGS / Credit Stock (same pair as the delivery-note engine).
                AddStockGlLine(glEntries, invoice, company, customer.Id,
                    cogsAccount.Id, debit: lineCost, credit: 0m, "Sales Invoice - COGS");
                AddStockGlLine(glEntries, invoice, company, customer.Id,
                    stockAccount.Id, debit: 0m, credit: lineCost, "Sales Invoice - Stock issue");
            }
        }

        await _stock.AddLedgerEntriesAsync(ledger, token);
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
