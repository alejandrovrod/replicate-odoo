using Erp.Application.Common;
using Erp.Application.Features.Crm.DTOs;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Crm.Queries;

/// <summary>Assembles <see cref="GetLeadsQuery"/> from <see cref="ICrmRepository"/>.</summary>
public sealed class GetLeadsQueryHandler : IQueryHandler<GetLeadsQuery, IReadOnlyList<LeadDto>>
{
    private readonly ICrmRepository _crm;

    public GetLeadsQueryHandler(ICrmRepository crm)
    {
        _crm = crm;
    }

    public async Task<IReadOnlyList<LeadDto>> HandleAsync(
        GetLeadsQuery query,
        CancellationToken cancellationToken = default)
    {
        var limit = query.Limit <= 0 ? 50 : Math.Min(query.Limit, 500);
        var leads = await _crm.ListLeadsAsync(query.CompanyId, limit, cancellationToken);
        return leads.Select(LeadDto.Build).ToList();
    }
}
