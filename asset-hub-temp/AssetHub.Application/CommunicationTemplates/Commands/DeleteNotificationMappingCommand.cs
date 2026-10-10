using System;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.CommunicationTemplates.Commands;

public record DeleteNotificationMappingCommand(Guid Id) : IRequest<bool>;

public class DeleteNotificationMappingCommandHandler : IRequestHandler<DeleteNotificationMappingCommand, bool>
{
    private readonly ITenantDbContext _dbContext;

    public DeleteNotificationMappingCommandHandler(ITenantDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> Handle(DeleteNotificationMappingCommand request, CancellationToken cancellationToken)
    {
        var mapping = await _dbContext.NotificationMappings
            .FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken);

        if (mapping == null) return false;

        _dbContext.NotificationMappings.Remove(mapping);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
