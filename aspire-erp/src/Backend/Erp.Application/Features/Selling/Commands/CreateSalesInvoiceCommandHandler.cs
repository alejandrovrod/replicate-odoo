using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Selling.Commands;

public sealed class CreateSalesInvoiceCommandHandler : ICommandHandler<CreateSalesInvoiceCommand, Result<SalesInvoiceDto>>
{
    private readonly ISalesInvoiceRepository _salesInvoices;
    private readonly ICustomerRepository _customers;
    private readonly IItemRepository _items;
    private readonly ICompanyRepository _companies;

    public CreateSalesInvoiceCommandHandler(
        ISalesInvoiceRepository salesInvoices,
        ICustomerRepository customers,
        IItemRepository items,
        ICompanyRepository companies)
    {
        _salesInvoices = salesInvoices;
        _customers = customers;
        _items = items;
        _companies = companies;
    }

    public async Task<Result<SalesInvoiceDto>> HandleAsync(CreateSalesInvoiceCommand request, CancellationToken cancellationToken)
    {
        var company = await _companies.GetByIdAsync(request.CompanyId, cancellationToken);
        if (company is null) return Result<SalesInvoiceDto>.Failure(SellingErrorCodes.CompanyNotFound, "Company not found.");

        company.EnsurePostingDateUnlocked(request.PostingDate);
        // R-13 FC-04: closed fiscal year rejects the posting too (second half of plan.md §3).
        await _companies.EnsurePostingDateInOpenYearAsync(company.Id, request.PostingDate, cancellationToken);

        var customer = await _customers.GetByIdAsync(request.CustomerId, cancellationToken);
        if (customer is null) return Result<SalesInvoiceDto>.Failure(SellingErrorCodes.CustomerNotFound, "Customer not found.");

        var invoiceNumber = await _salesInvoices.GenerateNextInvoiceNumberAsync(cancellationToken);

        var invoice = new SalesInvoice
        {
            Id = Guid.NewGuid(),
            TenantId = company.TenantId,
            CompanyId = request.CompanyId,
            InvoiceNumber = invoiceNumber,
            CustomerId = request.CustomerId,
            PostingDate = request.PostingDate,
            DueDate = request.PostingDate.AddDays(customer.PaymentTermsDays),
            Status = SalesInvoiceStatus.Draft,
            IsPOS = false,
            UpdateStock = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        decimal netTotal = 0;

        foreach (var item in request.Items)
        {
            var domainItem = await _items.GetByIdAsync(item.ItemId, cancellationToken);
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
        }

        invoice.NetTotal = netTotal;
        invoice.TaxTotal = 0; // Simplified
        invoice.GrandTotal = netTotal;
        invoice.OutstandingAmount = netTotal; // Drafts start with outstanding equal to total

        await _salesInvoices.AddAsync(invoice, cancellationToken);

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
    }
}
