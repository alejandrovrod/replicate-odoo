using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Buying.Queries;

/// <summary>Assembles <see cref="GetSuppliersQuery"/> from <see cref="ISupplierRepository"/>.</summary>
public sealed class GetSuppliersQueryHandler : IQueryHandler<GetSuppliersQuery, PagedResult<SupplierDto>>
{
    private readonly ISupplierRepository _suppliers;

    public GetSuppliersQueryHandler(ISupplierRepository suppliers)
    {
        _suppliers = suppliers;
    }

    public async Task<PagedResult<SupplierDto>> HandleAsync(
        GetSuppliersQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = await _suppliers.GetRecentAsync(
            new PagedRequest(query.PageNumber, query.PageSize),
            cancellationToken);
        return page.Map(page.Items.Select(SupplierDto.From).ToList());
    }
}
