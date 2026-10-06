using Erp.Application.Common;
using Erp.Application.Features.Crm.DTOs;
using Erp.Domain.Common;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Crm.Queries;

/// <summary>Assembles <see cref="GetOpportunitiesQuery"/> from <see cref="ICrmRepository"/>.</summary>
public sealed class GetOpportunitiesQueryHandler : IQueryHandler<GetOpportunitiesQuery, PagedResult<OpportunityDto>>
{
    private readonly ICrmRepository _crm;

    public GetOpportunitiesQueryHandler(ICrmRepository crm)
    {
        _crm = crm;
    }

    public async Task<PagedResult<OpportunityDto>> HandleAsync(
        GetOpportunitiesQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = await _crm.ListOpportunitiesAsync(
            query.CompanyId,
            new PagedRequest(query.PageNumber, query.PageSize),
            query.Stage,
            cancellationToken);
        return page.Map(page.Items.Select(OpportunityDto.Build).ToList());
    }
}
