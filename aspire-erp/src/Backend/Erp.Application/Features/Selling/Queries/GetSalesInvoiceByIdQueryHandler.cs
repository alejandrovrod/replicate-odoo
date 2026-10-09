using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Selling.Queries;

/// <summary>Single-invoice read of <see cref="GetSalesInvoiceByIdQuery"/> (null = 404 at the API).</summary>
public sealed class GetSalesInvoiceByIdQueryHandler
    : IQueryHandler<GetSalesInvoiceByIdQuery, SalesInvoiceDto?>
{
    private readonly ISalesInvoiceRepository _salesInvoices;

    public GetSalesInvoiceByIdQueryHandler(ISalesInvoiceRepository salesInvoices)
    {
        _salesInvoices = salesInvoices;
    }

    public async Task<SalesInvoiceDto?> HandleAsync(
        GetSalesInvoiceByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var invoice = await _salesInvoices.GetByIdAsync(query.SalesInvoiceId, cancellationToken);
        if (invoice is null || invoice.CompanyId != query.CompanyId)
        {
            return null;
        }

        return SalesInvoiceDto.Build(invoice);
    }
}
