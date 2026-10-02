using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Buying.Queries;

/// <summary>
/// Loads the most recent suppliers of the tenant - the picker behind new purchase orders.
/// Suppliers are tenant-wide (like items), so no company filter applies. Defaults to 50.
/// </summary>
public sealed record GetSuppliersQuery(int Limit = 50) : IQuery<IReadOnlyList<SupplierDto>>;
