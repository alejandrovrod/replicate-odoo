using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Buying.Queries;

/// <summary>Assembles <see cref="GetSupplierGroupsQuery"/> from <see cref="ISupplierGroupRepository"/>.</summary>
public sealed class GetSupplierGroupsQueryHandler
    : IQueryHandler<GetSupplierGroupsQuery, IReadOnlyList<SupplierGroupDto>>
{
    private readonly ISupplierGroupRepository _groups;

    public GetSupplierGroupsQueryHandler(ISupplierGroupRepository groups)
    {
        _groups = groups;
    }

    public async Task<IReadOnlyList<SupplierGroupDto>> HandleAsync(
        GetSupplierGroupsQuery query,
        CancellationToken cancellationToken = default)
    {
        var groups = await _groups.GetActiveAsync(cancellationToken);
        return groups.Select(SupplierGroupDto.From).ToList();
    }
}
