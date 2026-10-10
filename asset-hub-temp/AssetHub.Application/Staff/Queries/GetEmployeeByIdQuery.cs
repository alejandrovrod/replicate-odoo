using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Staff.Queries;

public class GetEmployeeByIdQuery : IRequest<EmployeeDetailDto?>
{
    public Guid EmployeeId { get; set; }
}

public class EmployeeDetailDto
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string PreferredLocale { get; set; } = "es";
    public Guid RoleCatalogItemId { get; set; }
    public string? RoleLabel { get; set; }
    public Guid[] Skills { get; set; } = Array.Empty<Guid>();
    public Guid? UserId { get; set; }
    public bool IsActive { get; set; }
    public List<AvailabilitySlotDto> Availability { get; set; } = new();
    public List<TeamMembershipDto> Teams { get; set; } = new();
}

public class AvailabilitySlotDto
{
    public int DayOfWeek { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public bool IsAvailable { get; set; }
}

public class TeamMembershipDto
{
    public Guid TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public bool IsLead { get; set; }
}

public class GetEmployeeByIdQueryHandler : IRequestHandler<GetEmployeeByIdQuery, EmployeeDetailDto?>
{
    private readonly ITenantDbContext _db;

    public GetEmployeeByIdQueryHandler(ITenantDbContext db)
    {
        _db = db;
    }

    public async Task<EmployeeDetailDto?> Handle(GetEmployeeByIdQuery request, CancellationToken cancellationToken)
    {
        var emp = await _db.Employees
            .AsNoTracking()
            .Include(e => e.RoleCatalogItem)
                .ThenInclude(ci => ci!.Translations)
            .FirstOrDefaultAsync(e => e.Id == request.EmployeeId && !e.IsDeleted, cancellationToken);

        if (emp == null) return null;

        var availability = await _db.EmployeeAvailabilities
            .AsNoTracking()
            .Where(a => a.EmployeeId == request.EmployeeId)
            .OrderBy(a => a.DayOfWeek).ThenBy(a => a.StartTime)
            .Select(a => new AvailabilitySlotDto
            {
                DayOfWeek = a.DayOfWeek,
                StartTime = a.StartTime,
                EndTime = a.EndTime,
                IsAvailable = a.IsAvailable
            })
            .ToListAsync(cancellationToken);

        var teams = await _db.TeamMembers
            .AsNoTracking()
            .Include(tm => tm.Team)
            .Where(tm => tm.EmployeeId == request.EmployeeId && !tm.Team!.IsDeleted)
            .Select(tm => new TeamMembershipDto
            {
                TeamId = tm.TeamId,
                TeamName = tm.Team!.Name,
                IsLead = tm.IsLead
            })
            .ToListAsync(cancellationToken);

        Guid[] skills = Array.Empty<Guid>();
        try
        {
            skills = JsonSerializer.Deserialize<Guid[]>(emp.SkillsJson) ?? Array.Empty<Guid>();
        }
        catch { /* ignore parse errors */ }

        return new EmployeeDetailDto
        {
            Id = emp.Id,
            FirstName = emp.FirstName,
            LastName = emp.LastName,
            Email = emp.Email,
            PhoneNumber = emp.PhoneNumber,
            PreferredLocale = emp.PreferredLocale,
            RoleCatalogItemId = emp.RoleCatalogItemId,
            RoleLabel = emp.RoleCatalogItem?.Translations
                .Where(t => t.Locale == "es")
                .Select(t => t.Label)
                .FirstOrDefault(),
            Skills = skills,
            UserId = emp.UserId,
            IsActive = emp.IsActive,
            Availability = availability,
            Teams = teams
        };
    }
}
