using Erp.Application.Common;
using Erp.Application.Features.Crm.DTOs;
using Erp.Domain.Common;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Crm.Queries;

/// <summary>Assembles <see cref="GetLeadsQuery"/> from <see cref="ICrmRepository"/>.</summary>
public sealed class GetLeadsQueryHandler : IQueryHandler<GetLeadsQuery, PagedResult<LeadDto>>
{
    private readonly ICrmRepository _crm;

    public GetLeadsQueryHandler(ICrmRepository crm)
    {
        _crm = crm;
    }

    public async Task<PagedResult<LeadDto>> HandleAsync(
        GetLeadsQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = await _crm.ListLeadsAsync(
            query.CompanyId,
            new PagedRequest(query.PageNumber, query.PageSize),
            cancellationToken);
        return page.Map(page.Items.Select(LeadDto.Build).ToList());
    }
}
