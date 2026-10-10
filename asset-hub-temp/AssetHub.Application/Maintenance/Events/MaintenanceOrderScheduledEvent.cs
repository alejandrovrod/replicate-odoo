using System;
using MediatR;

namespace AssetHub.Application.Maintenance.Events;

public record MaintenanceOrderScheduledEvent(
    Guid MaintenanceOrderId,
    Guid TenantId,
    Guid AssetId,
    Guid? IncidentId
) : INotification;
