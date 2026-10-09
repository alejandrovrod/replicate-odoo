using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Selling.Queries;

/// <summary>
/// Assembles <see cref="GetSalesInvoicesQuery"/> from <see cref="ISalesInvoiceRepository"/>.
/// Tenant isolation is automatic (Constitution II.3).
/// </summary>
public sealed class GetSalesInvoicesQueryHandler
    : IQueryHandler<GetSalesInvoicesQuery, PagedResult<SalesInvoiceDto>>
{
    private readonly ISalesInvoiceRepository _salesInvoices;

    public GetSalesInvoicesQueryHandler(ISalesInvoiceRepository salesInvoices)
    {
        _salesInvoices = salesInvoices;
    }

    public async Task<PagedResult<SalesInvoiceDto>> HandleAsync(
        GetSalesInvoicesQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = await _salesInvoices.GetRecentByCompanyAsync(
            query.CompanyId,
            new PagedRequest(query.PageNumber, query.PageSize),
            cancellationToken);
        if (page.Items.Count == 0)
        {
            return page.Map(new List<SalesInvoiceDto>());
        }

        var result = new List<SalesInvoiceDto>(page.Items.Count);
        foreach (var invoice in page.Items)
        {
            result.Add(SalesInvoiceDto.Build(invoice));
        }

        return page.Map(result);
    }
}
