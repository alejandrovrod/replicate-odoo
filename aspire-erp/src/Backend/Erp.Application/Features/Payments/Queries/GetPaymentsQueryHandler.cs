using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Payments.Queries;

public sealed class GetPaymentsQueryHandler
    : IQueryHandler<GetPaymentsQuery, PagedResult<PaymentEntryDto>>
{
    private readonly IBankRepository _banks;

    public GetPaymentsQueryHandler(IBankRepository banks)
    {
        _banks = banks;
    }

    public async Task<PagedResult<PaymentEntryDto>> HandleAsync(
        GetPaymentsQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = await _banks.GetPaymentsPagedAsync(
            query.CompanyId,
            new PagedRequest(query.Page, query.PageSize),
            query.Status,
            query.PaymentType,
            cancellationToken);

        return new PagedResult<PaymentEntryDto>(
            page.Items.Select(PaymentEntryDto.From).ToList(),
            page.TotalCount,
            page.PageNumber,
            page.PageSize);
    }
}

public sealed class GetPaymentDetailQueryHandler
    : IQueryHandler<GetPaymentDetailQuery, PaymentEntryDetailDto?>
{
    private readonly IBankRepository _banks;

    public GetPaymentDetailQueryHandler(IBankRepository banks)
    {
        _banks = banks;
    }

    public async Task<PaymentEntryDetailDto?> HandleAsync(
        GetPaymentDetailQuery query,
        CancellationToken cancellationToken = default)
    {
        var payment = await _banks.GetPaymentByIdAsync(query.PaymentId, cancellationToken);
        if (payment is null || payment.CompanyId != query.CompanyId)
        {
            return null;
        }

        return new PaymentEntryDetailDto(
            PaymentEntryDto.From(payment),
            payment.Allocations
                .OrderBy(a => a.AllocatedAmount)
                .Select(PaymentAllocationDto.From)
                .ToList());
    }
}

public sealed class GetOutstandingSalesInvoicesQueryHandler
    : IQueryHandler<GetOutstandingSalesInvoicesQuery, IReadOnlyList<OutstandingInvoiceDto>>
{
    private readonly ISalesInvoiceRepository _salesInvoices;

    public GetOutstandingSalesInvoicesQueryHandler(ISalesInvoiceRepository salesInvoices)
    {
        _salesInvoices = salesInvoices;
    }

    public async Task<IReadOnlyList<OutstandingInvoiceDto>> HandleAsync(
        GetOutstandingSalesInvoicesQuery query,
        CancellationToken cancellationToken = default)
    {
        var invoices = await _salesInvoices.GetOutstandingByCustomerAsync(
            query.CompanyId, query.CustomerId, cancellationToken);

        return invoices
            .Select(i => new OutstandingInvoiceDto(
                i.Id,
                i.InvoiceNumber,
                i.PostingDate,
                i.DueDate,
                i.GrandTotal,
                i.OutstandingAmount,
                i.Status.ToString()))
            .ToList();
    }
}

public sealed class GetOutstandingPurchaseInvoicesQueryHandler
    : IQueryHandler<GetOutstandingPurchaseInvoicesQuery, IReadOnlyList<OutstandingInvoiceDto>>
{
    private readonly IPurchaseRepository _purchases;

    public GetOutstandingPurchaseInvoicesQueryHandler(IPurchaseRepository purchases)
    {
        _purchases = purchases;
    }

    public async Task<IReadOnlyList<OutstandingInvoiceDto>> HandleAsync(
        GetOutstandingPurchaseInvoicesQuery query,
        CancellationToken cancellationToken = default)
    {
        var bills = await _purchases.GetOutstandingBySupplierAsync(
            query.CompanyId, query.SupplierId, cancellationToken);

        return bills
            .Select(b => new OutstandingInvoiceDto(
                b.Id,
                b.BillNumber,
                b.PostingDate,
                b.DueDate,
                b.GrandTotal,
                b.OutstandingAmount,
                b.Status.ToString()))
            .ToList();
    }
}
