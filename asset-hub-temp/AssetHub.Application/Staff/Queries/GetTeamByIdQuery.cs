using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Staff.Queries;

public class GetTeamByIdQuery : IRequest<TeamDetailDto?>
{
    public Guid TeamId { get; set; }
}

public class TeamDetailDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<TeamMemberDetailDto> Members { get; set; } = new();
}

public class TeamMemberDetailDto
{
    public Guid EmployeeId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsLead { get; set; }
    public bool IsActive { get; set; }
}

public class GetTeamByIdQueryHandler : IRequestHandler<GetTeamByIdQuery, TeamDetailDto?>
{
    private readonly ITenantDbContext _db;

    public GetTeamByIdQueryHandler(ITenantDbContext db)
    {
        _db = db;
    }

    public async Task<TeamDetailDto?> Handle(GetTeamByIdQuery request, CancellationToken cancellationToken)
    {
        var team = await _db.Teams
            .AsNoTracking()
            .Include(t => t.Members)
                .ThenInclude(m => m.Employee)
            .FirstOrDefaultAsync(t => t.Id == request.TeamId && !t.IsDeleted, cancellationToken);

        if (team == null) return null;

        return new TeamDetailDto
        {
            Id = team.Id,
            Name = team.Name,
            Description = team.Description,
            Members = team.Members
                .Where(m => m.Employee != null)
                .Select(m => new TeamMemberDetailDto
                {
                    EmployeeId = m.EmployeeId,
                    FirstName = m.Employee!.FirstName,
                    LastName = m.Employee!.LastName,
                    Email = m.Employee!.Email,
                    IsLead = m.IsLead,
                    IsActive = m.Employee!.IsActive
                })
                .OrderByDescending(m => m.IsLead)
                .ThenBy(m => m.FirstName)
                .ToList()
        };
    }
}
