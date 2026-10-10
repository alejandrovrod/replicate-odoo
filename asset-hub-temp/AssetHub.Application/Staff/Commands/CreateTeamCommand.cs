using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Staff;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Staff.Commands;

public class CreateTeamCommand : IRequest<Guid>
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    
    public List<MemberDto> Members { get; set; } = new();

    public class MemberDto
    {
        public Guid EmployeeId { get; set; }
        public bool IsLead { get; set; }
    }
}

public class CreateTeamCommandHandler : IRequestHandler<CreateTeamCommand, Guid>
{
    private readonly ITenantDbContext _db;
    private readonly ITenantResolver _tenantResolver;

    public CreateTeamCommandHandler(ITenantDbContext db, ITenantResolver tenantResolver)
    {
        _db = db;
        _tenantResolver = tenantResolver;
    }

    public async Task<Guid> Handle(CreateTeamCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId();

        if (request.Members.Count == 0)
            throw new ArgumentException("Team must have at least one member");

        var team = new Team
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Value,
            Name = request.Name,
            Description = request.Description
        };

        foreach (var memberDto in request.Members)
        {
            var empExists = await _db.Employees.AnyAsync(e => e.Id == memberDto.EmployeeId, cancellationToken);
            if (!empExists)
                throw new ArgumentException($"Employee {memberDto.EmployeeId} not found");

            team.Members.Add(new TeamMember
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId.Value,
                TeamId = team.Id,
                EmployeeId = memberDto.EmployeeId,
                IsLead = memberDto.IsLead
            });
        }

        try
        {
            _db.Teams.Add(team);
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            var entries = ex.Entries.Select(e => e.Entity.GetType().Name + " " + e.State).ToList();
            throw new Exception($"Concurrency error in CreateTeam. Entries: {string.Join(", ", entries)}", ex);
        }

        return team.Id;
    }
}
