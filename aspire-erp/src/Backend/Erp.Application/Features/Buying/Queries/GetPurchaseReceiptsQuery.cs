using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;

namespace Erp.Application.Features.Buying.Queries;

/// <summary>
/// Loads the most recent purchase receipts of one company with their lines (including the line
/// ids invoice lines bill against) - the audit view behind Task 4.2. Paginated (Standard
/// Pagination Pattern): page 1 of 50 by default.
/// </summary>
public sealed record GetPurchaseReceiptsQuery(Guid CompanyId, int PageNumber = 1, int PageSize = 50)
    : IQuery<PagedResult<PurchaseReceiptDto>>;
