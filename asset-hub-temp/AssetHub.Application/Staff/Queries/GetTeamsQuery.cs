using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Staff.Queries;

public class GetTeamsQuery : IRequest<GetTeamsResult>
{
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class GetTeamsResult
{
    public List<TeamSummaryDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class TeamSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int MemberCount { get; set; }
    public string? LeadName { get; set; }
}

public class GetTeamsQueryHandler : IRequestHandler<GetTeamsQuery, GetTeamsResult>
{
    private readonly ITenantDbContext _db;

    public GetTeamsQueryHandler(ITenantDbContext db)
    {
        _db = db;
    }

    public async Task<GetTeamsResult> Handle(GetTeamsQuery request, CancellationToken cancellationToken)
    {
        var query = _db.Teams
            .AsNoTracking()
            .Include(t => t.Members)
                .ThenInclude(m => m.Employee)
            .Where(t => !t.IsDeleted)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLowerInvariant();
            query = query.Where(t => t.Name.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(t => t.Name)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(t => new TeamSummaryDto
            {
                Id = t.Id,
                Name = t.Name,
                Description = t.Description,
                MemberCount = t.Members.Count,
                LeadName = t.Members
                    .Where(m => m.IsLead && m.Employee != null)
                    .Select(m => $"{m.Employee!.FirstName} {m.Employee!.LastName}")
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return new GetTeamsResult
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
