using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Buying.Queries;

/// <summary>Single-supplier read of <see cref="GetSupplierByIdQuery"/> (null = 404 at the API).</summary>
public sealed class GetSupplierByIdQueryHandler : IQueryHandler<GetSupplierByIdQuery, SupplierDto?>
{
    private readonly ISupplierRepository _suppliers;

    public GetSupplierByIdQueryHandler(ISupplierRepository suppliers)
    {
        _suppliers = suppliers;
    }

    public async Task<SupplierDto?> HandleAsync(
        GetSupplierByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var supplier = await _suppliers.GetByIdAsync(query.SupplierId, cancellationToken);
        return supplier is null ? null : SupplierDto.From(supplier);
    }
}
