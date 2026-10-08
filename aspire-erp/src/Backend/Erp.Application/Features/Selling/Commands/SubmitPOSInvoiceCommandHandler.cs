using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Selling.Commands;

public sealed class SubmitPOSInvoiceCommandHandler : ICommandHandler<SubmitPOSInvoiceCommand, Result<SalesInvoiceDto>>
{
    private readonly ISalesInvoiceRepository _salesInvoiceRepository;
    private readonly IPOSProfileRepository _posProfileRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IItemRepository _itemRepository;
    private readonly IStockRepository _stockRepository;
    private readonly ICompanyRepository _companyRepository;

    public SubmitPOSInvoiceCommandHandler(
        ISalesInvoiceRepository salesInvoiceRepository,
        IPOSProfileRepository posProfileRepository,
        ICustomerRepository customerRepository,
        IItemRepository itemRepository,
        IStockRepository stockRepository,
        ICompanyRepository companyRepository)
    {
        _salesInvoiceRepository = salesInvoiceRepository;
        _posProfileRepository = posProfileRepository;
        _customerRepository = customerRepository;
        _itemRepository = itemRepository;
        _stockRepository = stockRepository;
        _companyRepository = companyRepository;
    }

    public async Task<Result<SalesInvoiceDto>> HandleAsync(SubmitPOSInvoiceCommand request, CancellationToken cancellationToken)
    {
        return await _salesInvoiceRepository.ExecuteInTransactionAsync(async token =>
        {
            var company = await _companyRepository.GetByIdAsync(request.CompanyId, token);
            if (company is null) return Result<SalesInvoiceDto>.Failure(SellingErrorCodes.CompanyNotFound, "Company not found.");

            company.EnsurePostingDateUnlocked(request.PostingDate);
            // R-13 FC-04: closed fiscal year rejects the posting too (second half of plan.md §3).
            await _companyRepository.EnsurePostingDateInOpenYearAsync(company.Id, request.PostingDate, token);

            var profile = await _posProfileRepository.GetByIdAsync(request.POSProfileId, token);
            if (profile is null || !profile.IsActive) return Result<SalesInvoiceDto>.Failure(SellingErrorCodes.POSProfileNotFound, "POS Profile not found or inactive.");

            var customer = await _customerRepository.GetByIdAsync(request.CustomerId, token);
            if (customer is null) return Result<SalesInvoiceDto>.Failure(SellingErrorCodes.CustomerNotFound, "Customer not found.");

            var invoiceNumber = await _salesInvoiceRepository.GenerateNextInvoiceNumberAsync(token);

            var invoice = new SalesInvoice
            {
                Id = Guid.NewGuid(),
                TenantId = company.TenantId,
                CompanyId = request.CompanyId,
                InvoiceNumber = invoiceNumber,
                CustomerId = request.CustomerId,
                PostingDate = request.PostingDate,
                DueDate = request.PostingDate,
                Status = SalesInvoiceStatus.Paid,
                IsPOS = true,
                UpdateStock = true,
                SourceWarehouseId = profile.WarehouseId,
                CreatedAt = DateTimeOffset.UtcNow
            };

            decimal netTotal = 0;
            var sles = new List<StockLedgerEntry>();

            foreach (var item in request.Items)
            {
                var domainItem = await _itemRepository.GetByIdAsync(item.ItemId, token);
                if (domainItem is null) return Result<SalesInvoiceDto>.Failure(SellingErrorCodes.ItemNotFound, $"Item {item.ItemId} not found.");

                var amount = item.Quantity * item.Rate;
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

                // Generate StockLedgerEntry
                sles.Add(new StockLedgerEntry
                {
                    Id = Guid.NewGuid(),
                    TenantId = company.TenantId,
                    ItemId = item.ItemId,
                    WarehouseId = profile.WarehouseId,
                    VoucherType = "Sales Invoice",
                    VoucherNo = invoice.InvoiceNumber,
                    PostingDate = request.PostingDate,
                    QtyChange = -item.Quantity,
                    ValuationRate = item.Rate, // Ideally comes from valuation, using Rate for simplicity here
                    Amount = -(item.Quantity * item.Rate),
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }

            invoice.NetTotal = netTotal;
            invoice.TaxTotal = 0; // Simplified for this example
            invoice.GrandTotal = netTotal;
            
            decimal paidAmount = 0;
            var glEntries = new List<GLEntry>();

            foreach (var payment in request.Payments)
            {
                paidAmount += payment.Amount;
                var accountId = payment.ModeOfPayment == "Cash" ? profile.CashAccountId : profile.CardClearingAccountId;

                glEntries.Add(new GLEntry
                {
                    TenantId = company.TenantId,
                    CompanyId = request.CompanyId,
                    PostingDate = request.PostingDate,
                    AccountId = accountId,
                    Debit = payment.Amount,
                    Credit = 0,
                    VoucherType = "Sales Invoice",
                    VoucherNo = invoice.InvoiceNumber,
                    VoucherId = invoice.Id,
                    PartyType = "Customer",
                    PartyId = customer.Id,
                    Remarks = $"POS Payment ({payment.ModeOfPayment})",
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }

            if (paidAmount != invoice.GrandTotal)
                return Result<SalesInvoiceDto>.Failure(SellingErrorCodes.ValidationFailed, "Paid amount must equal Grand Total for POS.");

            invoice.PaidAmount = paidAmount;
            invoice.OutstandingAmount = 0;

            // Credit Sales Revenue
            glEntries.Add(new GLEntry
            {
                TenantId = company.TenantId,
                CompanyId = request.CompanyId,
                PostingDate = request.PostingDate,
                AccountId = profile.IncomeAccountId,
                Debit = 0,
                Credit = invoice.NetTotal,
                VoucherType = "Sales Invoice",
                VoucherNo = invoice.InvoiceNumber,
                VoucherId = invoice.Id,
                PartyType = "Customer",
                PartyId = customer.Id,
                Remarks = "POS Sales Revenue",
                CreatedAt = DateTimeOffset.UtcNow
            });

            await _salesInvoiceRepository.AddAsync(invoice, token);
            await _stockRepository.AddLedgerEntriesAsync(sles, token);
            await _salesInvoiceRepository.AddGlEntriesAsync(glEntries, token);

            var dto = new SalesInvoiceDto(
                invoice.Id, invoice.CompanyId, invoice.InvoiceNumber, invoice.CustomerId,
                customer.CustomerName, invoice.PostingDate, invoice.DueDate, invoice.Status,
                invoice.IsPOS, invoice.UpdateStock, invoice.SourceWarehouseId,
                invoice.NetTotal, invoice.TaxTotal, invoice.GrandTotal,
                invoice.OutstandingAmount, invoice.PaidAmount,
                Convert.ToBase64String(invoice.RowVersion ?? Array.Empty<byte>()),
                invoice.CreatedAt,
                invoice.Items.Select(i => new SalesInvoiceItemDto(i.Id, i.ItemId, i.SalesOrderItemId, i.Quantity, i.Rate, i.Amount)).ToList()
            );

            return Result<SalesInvoiceDto>.Success(dto);
        }, cancellationToken);
    }
}
