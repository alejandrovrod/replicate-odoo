using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.CommunicationTemplates;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.CommunicationTemplates.Queries;

public record NotificationMappingDto(Guid Id, string SystemEvent, Guid TemplateId, bool IsActive);

public record GetNotificationMappingsQuery : IRequest<List<NotificationMappingDto>>;

public class GetNotificationMappingsQueryHandler : IRequestHandler<GetNotificationMappingsQuery, List<NotificationMappingDto>>
{
    private readonly ITenantDbContext _dbContext;

    public GetNotificationMappingsQueryHandler(ITenantDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<NotificationMappingDto>> Handle(GetNotificationMappingsQuery request, CancellationToken cancellationToken)
    {
        return await _dbContext.NotificationMappings
            .Select(m => new NotificationMappingDto(m.Id, m.SystemEvent, m.TemplateId, m.IsActive))
            .ToListAsync(cancellationToken);
    }
}
