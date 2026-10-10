using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Common.Models;
using AssetHub.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Incidents.Queries;

public record IncidentSummaryDto(
    Guid Id,
    string Title,
    string State,
    Guid AssetId,
    string AssetName,
    DateTime ReportedAt,
    DateTime? ResolvedAt,
    DateTime? ClosedAt
);

public record SearchIncidentsQuery(string? SearchTerm, string? State, Dictionary<string, Guid>? CatalogFilters = null, Guid? AssetId = null, int Page = 1, int PageSize = 50) : IRequest<PagedResult<IncidentSummaryDto>>;

public class SearchIncidentsQueryHandler : IRequestHandler<SearchIncidentsQuery, PagedResult<IncidentSummaryDto>>
{
    private readonly ITenantDbContext _db;
    private readonly ITenantResolver _tenantResolver;

    public SearchIncidentsQueryHandler(ITenantDbContext db, ITenantResolver tenantResolver)
    {
        _db = db;
        _tenantResolver = tenantResolver;
    }

    public async Task<PagedResult<IncidentSummaryDto>> Handle(SearchIncidentsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId();
        var query = _db.Incidents
            .Include(i => i.Asset)
            .Where(i => i.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(request.State))
        {
            query = query.Where(i => i.State == request.State);
        }

        if (request.AssetId.HasValue)
        {
            query = query.Where(i => i.AssetId == request.AssetId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.ToLower();
            query = query.Where(i => i.Title.ToLower().Contains(term) 
                                  || (i.Asset != null && i.Asset.Name.ToLower().Contains(term))
                                  || (i.Asset != null && i.Asset.Code.ToLower().Contains(term)));
        }

        if (request.CatalogFilters != null && request.CatalogFilters.Any())
        {
            foreach (var filter in request.CatalogFilters)
            {
                var catalogItemIdStr = filter.Value.ToString();
                query = query.Where(a => a.PropertiesJson != null && a.PropertiesJson.Contains(catalogItemIdStr));
            }
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(i => i.ReportedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(i => new IncidentSummaryDto(
                i.Id,
                i.Title,
                i.State,
                i.AssetId,
                i.Asset != null ? i.Asset.Name : "",
                i.ReportedAt,
                i.ResolvedAt,
                i.ClosedAt
            ))
            .ToListAsync(cancellationToken);

        return new PagedResult<IncidentSummaryDto>(items, totalCount, request.Page, request.PageSize);
    }
}
