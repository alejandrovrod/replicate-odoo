using System;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.CommunicationTemplates;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.CommunicationTemplates.Commands;

public record UpdateNotificationMappingCommand(string SystemEvent, Guid TemplateId, bool IsActive) : IRequest<Guid>;

public class UpdateNotificationMappingCommandHandler : IRequestHandler<UpdateNotificationMappingCommand, Guid>
{
    private readonly ITenantDbContext _dbContext;
    private readonly ITenantResolver _tenantResolver;

    public UpdateNotificationMappingCommandHandler(ITenantDbContext dbContext, ITenantResolver tenantResolver)
    {
        _dbContext = dbContext;
        _tenantResolver = tenantResolver;
    }

    public async Task<Guid> Handle(UpdateNotificationMappingCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId() ?? throw new UnauthorizedAccessException("Tenant is required.");

        var mapping = await _dbContext.NotificationMappings
            .FirstOrDefaultAsync(m => m.SystemEvent == request.SystemEvent, cancellationToken);

        if (mapping == null)
        {
            mapping = new NotificationMapping
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SystemEvent = request.SystemEvent,
                TemplateId = request.TemplateId,
                IsActive = request.IsActive
            };
            _dbContext.NotificationMappings.Add(mapping);
        }
        else
        {
            mapping.TemplateId = request.TemplateId;
            mapping.IsActive = request.IsActive;
            mapping.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return mapping.Id;
    }
}
