using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;

namespace Erp.Application.Features.Selling.Queries;

/// <summary>
/// Loads the most recent sales invoices of one company with lines and customer identity.
/// Paginated (Standard Pagination Pattern): page 1 of 50 by default.
/// </summary>
public sealed record GetSalesInvoicesQuery(Guid CompanyId, int PageNumber = 1, int PageSize = 50)
    : IQuery<PagedResult<SalesInvoiceDto>>;
