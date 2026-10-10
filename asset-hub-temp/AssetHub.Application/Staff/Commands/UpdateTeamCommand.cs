using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Staff;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Staff.Commands;

public class UpdateTeamCommand : IRequest<Unit>
{
    public Guid TeamId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<MemberDto> Members { get; set; } = new();

    public class MemberDto
    {
        public Guid EmployeeId { get; set; }
        public bool IsLead { get; set; }
    }
}

public class UpdateTeamCommandHandler : IRequestHandler<UpdateTeamCommand, Unit>
{
    private readonly ITenantDbContext _db;
    private readonly ITenantResolver _tenantResolver;

    public UpdateTeamCommandHandler(ITenantDbContext db, ITenantResolver tenantResolver)
    {
        _db = db;
        _tenantResolver = tenantResolver;
    }

    public async Task<Unit> Handle(UpdateTeamCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId();

        var team = await _db.Teams
            .FirstOrDefaultAsync(t => t.Id == request.TeamId && !t.IsDeleted, cancellationToken);

        if (team == null)
            throw new ArgumentException("Team not found");

        if (request.Members.Count == 0)
            throw new ArgumentException("Team must have at least one member");

        // Validate all employees exist
        foreach (var memberDto in request.Members)
        {
            var empExists = await _db.Employees.AnyAsync(
                e => e.Id == memberDto.EmployeeId && !e.IsDeleted, cancellationToken);
            if (!empExists)
                throw new ArgumentException($"Employee {memberDto.EmployeeId} not found");
        }

        team.Name = request.Name;
        team.Description = request.Description;

        // Replace members: Delete old members from DB directly to avoid EF tracking issues
        await _db.TeamMembers
            .Where(tm => tm.TeamId == team.Id)
            .ExecuteDeleteAsync(cancellationToken);

        // Add fresh members
        foreach (var reqMember in request.Members)
        {
            _db.TeamMembers.Add(new TeamMember
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId!.Value,
                TeamId = team.Id,
                EmployeeId = reqMember.EmployeeId,
                IsLead = reqMember.IsLead
            });
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            var entries = ex.Entries.Select(e => e.Entity.GetType().Name + " " + e.State).ToList();
            throw new Exception($"Concurrency error in UpdateTeam. Entries: {string.Join(", ", entries)}", ex);
        }

        return Unit.Value;
    }
}
