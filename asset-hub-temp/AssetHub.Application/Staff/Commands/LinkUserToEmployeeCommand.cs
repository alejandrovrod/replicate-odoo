using System;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Staff.Commands;

public class LinkUserToEmployeeCommand : IRequest<Unit>
{
    public Guid EmployeeId { get; set; }
    public Guid UserId { get; set; }
}

public class LinkUserToEmployeeCommandHandler : IRequestHandler<LinkUserToEmployeeCommand, Unit>
{
    private readonly ITenantDbContext _db;

    public LinkUserToEmployeeCommandHandler(ITenantDbContext db)
    {
        _db = db;
    }

    public async Task<Unit> Handle(LinkUserToEmployeeCommand request, CancellationToken cancellationToken)
    {
        var emp = await _db.Employees.FirstOrDefaultAsync(e => e.Id == request.EmployeeId, cancellationToken);
        if (emp == null)
            throw new ArgumentException("Employee not found");

        // Validate if UserId is already linked
        var isLinked = await _db.Employees.AnyAsync(e => e.UserId == request.UserId, cancellationToken);
        if (isLinked)
            throw new InvalidOperationException("User is already linked to an employee");

        // We assume UserId validation exists outside or from identity scope
        emp.UserId = request.UserId;
        
        await _db.SaveChangesAsync(cancellationToken);
        
        return Unit.Value;
    }
}
