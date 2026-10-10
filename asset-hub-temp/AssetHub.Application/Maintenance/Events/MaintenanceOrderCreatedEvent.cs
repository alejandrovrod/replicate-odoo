using System;
using MediatR;

namespace AssetHub.Application.Maintenance.Events;

public record MaintenanceOrderCreatedEvent(
    Guid OrderId,
    Guid TenantId,
    Guid? AssetId,
    string PropertiesJson
) : INotification;
