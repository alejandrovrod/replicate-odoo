using Erp.Application.Common;
using Erp.Application.Features.Crm.DTOs;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Crm.Queries;

/// <summary>Assembles <see cref="GetOpportunitiesQuery"/> from <see cref="ICrmRepository"/>.</summary>
public sealed class GetOpportunitiesQueryHandler : IQueryHandler<GetOpportunitiesQuery, IReadOnlyList<OpportunityDto>>
{
    private readonly ICrmRepository _crm;

    public GetOpportunitiesQueryHandler(ICrmRepository crm)
    {
        _crm = crm;
    }

    public async Task<IReadOnlyList<OpportunityDto>> HandleAsync(
        GetOpportunitiesQuery query,
        CancellationToken cancellationToken = default)
    {
        var limit = query.Limit <= 0 ? 50 : Math.Min(query.Limit, 500);
        var opportunities = await _crm.ListOpportunitiesAsync(query.CompanyId, limit, cancellationToken);
        var filtered = string.IsNullOrWhiteSpace(query.Stage)
            ? opportunities
            : opportunities.Where(o => o.Stage == query.Stage).ToList();
        return filtered.Select(OpportunityDto.Build).ToList();
    }
}
