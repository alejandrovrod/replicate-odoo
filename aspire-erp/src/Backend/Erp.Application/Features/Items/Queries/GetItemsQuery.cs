using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Items.Queries;

/// <summary>
/// Loads the tenant's items together with their current stock inside the given company's
/// warehouses (qty + value per warehouse) - the data source of Task 3.4's <c>ItemList</c>.
/// Items are tenant-wide while stock is company-scoped (an item belongs to the warehouses that
/// hold it), so the company only limits the stock breakdown, never the item rows themselves.
/// </summary>
public sealed record GetItemsQuery(Guid CompanyId) : IQuery<IReadOnlyList<ItemDto>>;
