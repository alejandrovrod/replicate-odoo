using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Buying.Queries;

/// <summary>
/// Loads the tenant's supplier group catalog (module buying-suppliers, spec §5
/// <c>GET /api/v1/supplier-groups</c>): a small, shared list used to populate pickers.
/// </summary>
public sealed record GetSupplierGroupsQuery : IQuery<IReadOnlyList<SupplierGroupDto>>;
