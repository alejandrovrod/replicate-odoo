using System;
using MediatR;

namespace AssetHub.Application.Incidents.Events;

public record IncidentClosedEvent(
    Guid IncidentId,
    Guid AssetId,
    Guid TenantId,
    string FinalState
) : INotification;
