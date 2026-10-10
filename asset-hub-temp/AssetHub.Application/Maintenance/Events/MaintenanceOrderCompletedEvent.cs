using System;
using MediatR;

namespace AssetHub.Application.Maintenance.Events;

public record MaintenanceOrderCompletedEvent(
    Guid MaintenanceOrderId,
    Guid TenantId,
    Guid AssetId,
    Guid? IncidentId
) : INotification;
