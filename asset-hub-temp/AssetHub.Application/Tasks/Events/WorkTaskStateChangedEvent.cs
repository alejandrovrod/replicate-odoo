using System;
using MediatR;

namespace AssetHub.Application.Tasks.Events;

public record WorkTaskStateChangedEvent(
    Guid WorkTaskId,
    Guid TenantId,
    string FromState,
    string ToState,
    Guid? AssetId,
    Guid? IncidentId,
    Guid? MaintenanceOrderId,
    Guid? PreventivePlanId
) : INotification;
