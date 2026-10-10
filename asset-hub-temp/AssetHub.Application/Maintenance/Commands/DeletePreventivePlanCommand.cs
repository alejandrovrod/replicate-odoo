using System;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Maintenance.Commands;

public class DeletePreventivePlanCommand : IRequest
{
    public Guid Id { get; set; }
}

public class DeletePreventivePlanCommandHandler : IRequestHandler<DeletePreventivePlanCommand>
{
    private readonly ITenantDbContext _db;

    public DeletePreventivePlanCommandHandler(ITenantDbContext db)
    {
        _db = db;
    }

    public async Task Handle(DeletePreventivePlanCommand request, CancellationToken cancellationToken)
    {
        var plan = await _db.PreventivePlans
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (plan == null)
        {
            throw new ArgumentException($"Plan preventivo '{request.Id}' no encontrado.");
        }

        plan.IsDeleted = true;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
