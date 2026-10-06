using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Manufacturing.Queries;

/// <summary>
/// Assembles the Task 9.5 BOM reads from <see cref="IManufacturingRepository"/>: item codes and
/// names from <see cref="IItemRepository"/>, workstation names and composite hourly rates from
/// the manufacturing repository. Tenant isolation is automatic (Constitution II.3); CompanyId
/// is business scoping. Read-only: no stock, no GL, no idempotency guard.
/// </summary>
public sealed class GetBomsQueryHandler : IQueryHandler<GetBomsQuery, PagedResult<BomDto>>
{
    private readonly IManufacturingRepository _manufacturing;
    private readonly IItemRepository _items;

    public GetBomsQueryHandler(IManufacturingRepository manufacturing, IItemRepository items)
    {
        _manufacturing = manufacturing;
        _items = items;
    }

    public async Task<PagedResult<BomDto>> HandleAsync(
        GetBomsQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = await _manufacturing.ListBomsAsync(
            query.CompanyId,
            new PagedRequest(query.PageNumber, query.PageSize),
            cancellationToken);
        var items = await BomDtoAssembler.BuildAsync(page.Items, _manufacturing, _items, cancellationToken);
        return page.Map(items);
    }
}
