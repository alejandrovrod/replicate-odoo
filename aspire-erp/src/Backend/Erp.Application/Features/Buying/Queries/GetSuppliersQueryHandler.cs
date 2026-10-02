using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Buying.Queries;

/// <summary>Assembles <see cref="GetSuppliersQuery"/> from <see cref="ISupplierRepository"/>.</summary>
public sealed class GetSuppliersQueryHandler : IQueryHandler<GetSuppliersQuery, IReadOnlyList<SupplierDto>>
{
    private readonly ISupplierRepository _suppliers;

    public GetSuppliersQueryHandler(ISupplierRepository suppliers)
    {
        _suppliers = suppliers;
    }

    public async Task<IReadOnlyList<SupplierDto>> HandleAsync(
        GetSuppliersQuery query,
        CancellationToken cancellationToken = default)
    {
        var limit = query.Limit <= 0 ? 50 : Math.Min(query.Limit, 500);
        var suppliers = await _suppliers.GetRecentAsync(limit, cancellationToken);
        return suppliers.Select(SupplierDto.From).ToList();
    }
}
