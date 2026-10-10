using System;
using MediatR;

namespace AssetHub.Application.Incidents.Events;

public record IncidentStateChangedEvent(
    Guid IncidentId,
    Guid AssetId,
    string FromState,
    string ToState,
    bool IsTerminal
) : INotification;
