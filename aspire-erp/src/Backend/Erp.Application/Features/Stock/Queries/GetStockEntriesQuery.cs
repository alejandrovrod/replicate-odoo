using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Stock.Queries;

/// <summary>
/// Loads the most recent stock vouchers of one company with their lines - the list view behind
/// Task 3.4's stock entry screen. Defaults to the 50 newest vouchers.
/// </summary>
public sealed record GetStockEntriesQuery(Guid CompanyId, int Limit = 50) : IQuery<IReadOnlyList<StockEntryDto>>;
