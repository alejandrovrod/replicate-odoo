using System;
using MediatR;

namespace AssetHub.Application.Incidents.Events;

public record IncidentReportedEvent(
    Guid IncidentId,
    string Title,
    Guid AssetId,
    Guid TenantId,
    string PropertiesJson
) : INotification;
