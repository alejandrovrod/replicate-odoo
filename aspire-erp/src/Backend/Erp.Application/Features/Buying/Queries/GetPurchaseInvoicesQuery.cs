using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;

namespace Erp.Application.Features.Buying.Queries;

/// <summary>
/// Loads the most recent purchase invoices of one company with their lines - the vendor-bill
/// audit view behind Task 4.3. Paginated (Standard Pagination Pattern): page 1 of 50 by default.
/// </summary>
public sealed record GetPurchaseInvoicesQuery(Guid CompanyId, int PageNumber = 1, int PageSize = 50)
    : IQuery<PagedResult<PurchaseInvoiceDto>>;
