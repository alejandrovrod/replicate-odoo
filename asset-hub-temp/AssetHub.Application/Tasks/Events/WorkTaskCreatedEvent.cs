using System;
using MediatR;

namespace AssetHub.Application.Tasks.Events;

public record WorkTaskCreatedEvent(
    Guid TaskId,
    Guid TenantId,
    Guid? AssetId,
    string PropertiesJson
) : INotification;
