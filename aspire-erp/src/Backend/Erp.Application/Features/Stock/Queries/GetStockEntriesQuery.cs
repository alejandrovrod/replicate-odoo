using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;

namespace Erp.Application.Features.Stock.Queries;

/// <summary>
/// Loads the most recent stock vouchers of one company with their lines - the list view behind
/// Task 3.4's stock entry screen. Paginated (Standard Pagination Pattern): page 1 of 50 by default.
/// </summary>
public sealed record GetStockEntriesQuery(Guid CompanyId, int PageNumber = 1, int PageSize = 50) : IQuery<PagedResult<StockEntryDto>>;
