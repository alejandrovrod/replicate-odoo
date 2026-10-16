using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Services;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Selling.Commands;

public sealed class CreateSalesInvoiceCommandHandler : ICommandHandler<CreateSalesInvoiceCommand, Result<SalesInvoiceDto>>
{
    private readonly ISalesInvoiceRepository _salesInvoices;
    private readonly ICustomerRepository _customers;
    private readonly IItemRepository _items;
    private readonly ICompanyRepository _companies;
    private readonly IAccountRepository _accounts;
    private readonly IWarehouseRepository _warehouses;
    private readonly ITaxEngineService _taxEngine;
    private readonly ITaxTemplateRepository? _taxTemplates;

    public CreateSalesInvoiceCommandHandler(
        ISalesInvoiceRepository salesInvoices,
        ICustomerRepository customers,
        IItemRepository items,
        ICompanyRepository companies,
        IAccountRepository accounts,
        IWarehouseRepository warehouses,
        ITaxEngineService? taxEngine = null,
        ITaxTemplateRepository? taxTemplates = null)
    {
        _salesInvoices = salesInvoices;
        _customers = customers;
        _items = items;
        _companies = companies;
        _accounts = accounts;
        _warehouses = warehouses;
        _taxEngine = taxEngine ?? new TaxEngineService();
        _taxTemplates = taxTemplates;
    }

    public async Task<Result<SalesInvoiceDto>> HandleAsync(CreateSalesInvoiceCommand request, CancellationToken cancellationToken = default)
    {
        try
        {
            var company = await _companies.GetByIdAsync(request.CompanyId, cancellationToken);
            if (company is null) return Result<SalesInvoiceDto>.Failure(SellingErrorCodes.CompanyNotFound, "Company not found.");

            company.EnsurePostingDateUnlocked(request.PostingDate);
            // R-13 FC-04: closed fiscal year rejects the posting too (second half of plan.md §3).
            await _companies.EnsurePostingDateInOpenYearAsync(company.Id, request.PostingDate, cancellationToken);

            var customer = await _customers.GetByIdAsync(request.CustomerId, cancellationToken);
            if (customer is null) return Result<SalesInvoiceDto>.Failure(SellingErrorCodes.CustomerNotFound, "Customer not found.");

            if (request.Items is null || request.Items.Count == 0)
            {
                return Result<SalesInvoiceDto>.Failure(SellingErrorCodes.NoLines, "A sales document must contain at least one line.");
            }

            var invoiceNumber = await _salesInvoices.GenerateNextInvoiceNumberAsync(cancellationToken);

            var invoice = new SalesInvoice
            {
                Id = Guid.NewGuid(),
                TenantId = company.TenantId,
                CompanyId = request.CompanyId,
                InvoiceNumber = invoiceNumber,
                Customer = customer,
                CustomerId = request.CustomerId,
                PostingDate = request.PostingDate,
                DueDate = request.PostingDate.AddDays(customer.PaymentTermsDays),
                Status = SalesInvoiceStatus.Draft,
                IsPOS = false,
                IsReturn = request.IsReturn,
                ReturnAgainstId = request.ReturnAgainstId,
                UpdateStock = request.UpdateStock,
                SourceWarehouseId = request.SourceWarehouseId,
                CreatedAt = DateTimeOffset.UtcNow
            };

            if (request.UpdateStock)
            {
                await EnsureWarehouseAsync(request, company.Id, cancellationToken);
            }

            decimal netTotal = 0;

            foreach (var item in request.Items)
            {
                var domainItem = await _items.GetByIdAsync(item.ItemId, cancellationToken);
                if (domainItem is null) return Result<SalesInvoiceDto>.Failure(SellingErrorCodes.ItemNotFound, $"Item {item.ItemId} not found.");

                var amount = item.Quantity * item.Rate;
                // ERPNext validate_qty parity (both directions): returns demand negative lines,
                // normal invoices demand positive ones.
                SalesValidator.EnsureValidInvoiceLine(item.Quantity, item.Rate, amount, request.IsReturn);
                netTotal += amount;

                invoice.Items.Add(new SalesInvoiceItem
                {
                    Id = Guid.NewGuid(),
                    SalesInvoiceId = invoice.Id,
                    ItemId = item.ItemId,
                    Quantity = item.Quantity,
                    Rate = item.Rate,
                    Amount = amount
                });
            }

            // Module 17: global discount on the net base (ERPNext "apply on Net Total" default).
            // Percentage wins when no explicit amount travels; both supplied must agree.
            var discountAmount = request.DiscountAmount;
            if (request.DiscountPercentage > 0 && request.DiscountAmount == 0)
            {
                discountAmount = Math.Round(
                    Math.Abs(netTotal) * request.DiscountPercentage / 100m, 2, MidpointRounding.AwayFromZero);
            }

            SalesValidator.EnsureValidDiscount(request.DiscountPercentage, discountAmount, netTotal);

            invoice.DiscountPercentage = request.DiscountPercentage;
            invoice.DiscountAmount = discountAmount;

            // Module 17: taxes on the discounted base. Amounts are recomputed - never trusted.
            // Tax-engine Fase 2: template expansion (template lines first, then manual rows) +
            // Add/Deduct + cascade, computed by the shared engine at sales precision (2).
            var taxableBase = netTotal - discountAmount;
            var taxSpecs = await ResolveTaxSpecsAsync(request, company.Id, cancellationToken);
            var calculation = _taxEngine.Calculate(taxableBase, taxSpecs.Rows, decimals: 2);

            for (var i = 0; i < calculation.Rows.Count; i++)
            {
                var computed = calculation.Rows[i];
                var account = taxSpecs.Accounts[i];

                invoice.Taxes.Add(new SalesInvoiceTax
                {
                    Id = Guid.NewGuid(),
                    SalesInvoiceId = invoice.Id,
                    AccountId = account.Id,
                    Account = account,
                    Rate = taxSpecs.Rates[i],
                    ChargeType = computed.ChargeType,
                    CalculationBasis = computed.CalculationBasis,
                    BaseAmount = computed.BaseAmount,
                    TaxAmount = computed.TaxAmount,
                });
            }

            invoice.NetTotal = netTotal;
            invoice.TaxTotal = calculation.TaxTotal;
            invoice.GrandTotal = calculation.GrandTotal;

            // Module 18: return-mode guards (provenance, sign, ceiling vs the original invoice).
            if (request.IsReturn)
            {
                var ceiling = await EnsureReturnAgainstAsync(request, customer, company.Id, cancellationToken);
                if (Math.Abs(invoice.GrandTotal) - Math.Abs(ceiling) > 0.005m)
                {
                    return Result<SalesInvoiceDto>.Failure(
                        SellingErrorCodes.ReturnAmountExceeded,
                        $"Credit note total ({Math.Abs(invoice.GrandTotal):0.####}) cannot exceed "
                        + $"the original invoice total ({Math.Abs(ceiling):0.####}).");
                }
            }

            SalesValidator.EnsureInvoiceTotalsSign(invoice.NetTotal, invoice.TaxTotal, invoice.GrandTotal, request.IsReturn);

            invoice.OutstandingAmount = invoice.GrandTotal; // Drafts start with outstanding equal to total
            invoice.PaidAmount = 0;

            await _salesInvoices.AddAsync(invoice, cancellationToken);

            return Result<SalesInvoiceDto>.Success(SalesInvoiceDto.Build(invoice));
        }
        catch (SalesValidationException ex)
        {
            return Result<SalesInvoiceDto>.Failure(ex.Code, ex.Message);
        }
        catch (TaxValidationException ex)
        {
            // Tax-engine Fase 2: engine/template failures carry their own RFC 7807 codes
            // (tax_account_required, invalid_tax_calculation_basis, tax_template_not_found...).
            return Result<SalesInvoiceDto>.Failure(ex.Code, ex.Message);
        }
    }

    /// <summary>
    /// Module 17 task item (+ tax-engine Fase 2): tax accounts must be postable leaves of the
    /// company. <c>Add</c> rows keep the legacy LIABILITY rule (tax payable credited on
    /// submit); <c>Deduct</c> rows (suffered retentions, booked as debit) accept Liability
    /// (contra-liability, e.g. 21xx "retenciones a favor") OR Asset (recoverable) leaves.
    /// Revenue/income accounts are rejected here, not just in the SPA.
    /// </summary>
    private async Task<Account> RequireTaxAccountAsync(
        Guid accountId, Guid companyId, TaxChargeType chargeType, CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty)
        {
            throw new SalesValidationException(
                SellingErrorCodes.InvalidTaxAccount,
                "A tax row must reference a liability account (AccountId is required).");
        }

        var account = await _accounts.GetByIdAsync(accountId, cancellationToken);
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

    /// <summary>
    /// Tax-engine Fase 2: resolves the ordered computation specs (template lines first, then
    /// manual rows) with their validated accounts. The engine runs on the returned rows; the
    /// parallel <c>Accounts</c>/<c>Rates</c> lists persist the invoice rows in the same order.
    /// </summary>
    private async Task<(IReadOnlyList<TaxRowInput> Rows, IReadOnlyList<Account> Accounts, IReadOnlyList<decimal> Rates)>
        ResolveTaxSpecsAsync(CreateSalesInvoiceCommand request, Guid companyId, CancellationToken cancellationToken)
    {
        var rows = new List<TaxRowInput>();
        var accounts = new List<Account>();
        var rates = new List<decimal>();

        if (request.TaxTemplateId is { } templateId && templateId != Guid.Empty)
        {
            if (_taxTemplates is null)
            {
                throw new TaxValidationException(
                    TaxErrorCodes.TaxTemplateNotFound,
                    $"Tax template '{templateId}' was not found in this tenant.");
            }

            var template = await _taxTemplates.GetByIdAsync(templateId, cancellationToken)
                ?? throw new TaxValidationException(
                    TaxErrorCodes.TaxTemplateNotFound,
                    $"Tax template '{templateId}' was not found in this tenant.");

            if (template.CompanyId != companyId)
            {
                throw new TaxValidationException(
                    TaxErrorCodes.TaxTemplateNotFound,
                    $"Tax template '{template.Title}' does not belong to this company.");
            }

            if (!string.Equals(template.Category, "Sales", StringComparison.OrdinalIgnoreCase))
            {
                throw new TaxValidationException(
                    TaxErrorCodes.InvalidTaxTemplateCategory,
                    $"Tax template '{template.Title}' is a '{template.Category}' template and cannot be applied to a sales invoice.");
            }

            foreach (var line in template.Taxes)
            {
                if (line.AccountId == Guid.Empty)
                {
                    throw new TaxValidationException(
                        TaxErrorCodes.TaxAccountRequired,
                        $"Template '{template.Title}' line '{line.Description}' has no GL account assigned.");
                }

                accounts.Add(await RequireTaxAccountAsync(line.AccountId, companyId, line.ChargeType, cancellationToken));
                rates.Add(line.Rate);
                rows.Add(new TaxRowInput(line.Rate, line.ChargeType, line.CalculationBasis, line.Amount));
            }
        }

        foreach (var tax in request.Taxes ?? (IReadOnlyList<SalesInvoiceTaxCommandDto>)Array.Empty<SalesInvoiceTaxCommandDto>())
        {
            accounts.Add(await RequireTaxAccountAsync(tax.AccountId, companyId, tax.ChargeType, cancellationToken));
            rates.Add(tax.Rate);
            rows.Add(new TaxRowInput(tax.Rate, tax.ChargeType, tax.CalculationBasis, tax.FixedAmount));
        }

        return (rows, accounts, rates);
    }

    /// <summary>
    /// Module 18: the referenced original must exist in this company, belong to the same
    /// customer and be already submitted (Draft/Cancelled originals cannot be credited).
    /// Returns its GrandTotal as the credit ceiling.
    /// </summary>
    private async Task<decimal> EnsureReturnAgainstAsync(
        CreateSalesInvoiceCommand request, Customer customer, Guid companyId, CancellationToken cancellationToken)
    {
        if (!request.ReturnAgainstId.HasValue || request.ReturnAgainstId.Value == Guid.Empty)
        {
            throw new SalesValidationException(
                SellingErrorCodes.ReturnAgainstRequired,
                "A credit note (IsReturn) must reference the original invoice (ReturnAgainstId is required).");
        }

        var original = await _salesInvoices.GetByIdAsync(request.ReturnAgainstId.Value, cancellationToken);
        if (original is null
            || original.CompanyId != companyId
            || original.CustomerId != customer.Id
            || original.IsReturn)
        {
            throw new SalesValidationException(
                SellingErrorCodes.ReturnAgainstInvalid,
                $"ReturnAgainst invoice '{request.ReturnAgainstId}' was not found for this customer "
                + "or is itself a credit note.");
        }

        if (original.Status is not (SalesInvoiceStatus.Unpaid or SalesInvoiceStatus.PartiallyPaid or SalesInvoiceStatus.Paid))
        {
            throw new SalesValidationException(
                SellingErrorCodes.ReturnAgainstInvalid,
                $"ReturnAgainst invoice '{original.InvoiceNumber}' is '{original.Status}'; only a submitted invoice can be credited.");
        }

        return original.GrandTotal;
    }

    private async Task EnsureWarehouseAsync(
        CreateSalesInvoiceCommand request, Guid companyId, CancellationToken cancellationToken)
    {
        if (!request.SourceWarehouseId.HasValue || request.SourceWarehouseId.Value == Guid.Empty)
        {
            throw new SalesValidationException(
                SellingErrorCodes.ValidationFailed,
                "Invoices with UpdateStock must carry the source warehouse (SourceWarehouseId is required).");
        }

        var warehouse = await _warehouses.GetByIdAsync(request.SourceWarehouseId.Value, cancellationToken);
        if (warehouse is null || warehouse.CompanyId != companyId)
        {
            throw new SalesValidationException(
                SellingErrorCodes.ValidationFailed,
                $"Warehouse '{request.SourceWarehouseId}' was not found in this company.");
        }
    }
}
