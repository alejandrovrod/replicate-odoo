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

public class SetEmployeeAvailabilityCommand : IRequest<Unit>
{
    public Guid EmployeeId { get; set; }
    public List<AvailabilityDto> Availabilities { get; set; } = new();
    
    public class AvailabilityDto
    {
        public int DayOfWeek { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
    }
}

public class SetEmployeeAvailabilityCommandHandler : IRequestHandler<SetEmployeeAvailabilityCommand, Unit>
{
    private readonly ITenantDbContext _db;
    private readonly ITenantResolver _tenantResolver;

    public SetEmployeeAvailabilityCommandHandler(ITenantDbContext db, ITenantResolver tenantResolver)
    {
        _db = db;
        _tenantResolver = tenantResolver;
    }

    public async Task<Unit> Handle(SetEmployeeAvailabilityCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId();

        var emp = await _db.Employees.FirstOrDefaultAsync(e => e.Id == request.EmployeeId, cancellationToken);
        if (emp == null)
            throw new ArgumentException("Employee not found");

        foreach (var av in request.Availabilities)
        {
            if (av.EndTime <= av.StartTime)
                throw new ArgumentException($"Invalid time range: {av.StartTime} to {av.EndTime} for DayOfWeek {av.DayOfWeek}");
        }
        
        // Remove old availabilities
        var old = await _db.EmployeeAvailabilities.Where(ea => ea.EmployeeId == request.EmployeeId).ToListAsync(cancellationToken);
        _db.EmployeeAvailabilities.RemoveRange(old);

        // Add new
        foreach (var av in request.Availabilities)
        {
            _db.EmployeeAvailabilities.Add(new EmployeeAvailability
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId.Value,
                EmployeeId = emp.Id,
                DayOfWeek = av.DayOfWeek,
                StartTime = av.StartTime,
                EndTime = av.EndTime
            });
        }
        
        await _db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
