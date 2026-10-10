using System;
using MediatR;

namespace AssetHub.Application.Incidents.Events;

public record IncidentAssignedEvent(
    Guid IncidentId,
    Guid AssetId,
    Guid TenantId
) : INotification;
