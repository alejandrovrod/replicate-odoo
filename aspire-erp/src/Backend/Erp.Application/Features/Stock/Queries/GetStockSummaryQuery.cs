using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Stock.Queries;

/// <summary>
/// Company inventory aggregates for the overview stat cards - the Standard Pagination Pattern
/// companion to the paged list reads: scalar COUNT/SUM only, so cards never need the rows.
/// </summary>
public sealed record GetStockSummaryQuery(Guid CompanyId) : IQuery<StockSummaryDto>;
