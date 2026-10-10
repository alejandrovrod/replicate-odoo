using System;
using System.Collections.Generic;
using AssetHub.Domain.CommunicationTemplates;

namespace AssetHub.Application.CommunicationTemplates.Rendering;

/// <summary>
/// Catalogo estricto de variables disponibles por EntityScope.
/// Solo estas variables se exponen al motor de render; cualquier otra
/// referencia en la plantilla se reemplaza por string vacio.
/// </summary>
public static class TemplateVariableCatalog
{
    public static IReadOnlyList<string> ForScope(CommunicationEntityScope scope) => scope switch
    {
        CommunicationEntityScope.Incident => new[]
        {
            "incident.title", "incident.priority", "incident.state", "incident.type",
            "incident.reportedAt", "asset.name", "asset.code", "recipient.name"
        },
        CommunicationEntityScope.MaintenanceOrder => new[]
        {
            "order.id", "order.title", "order.kind", "order.state", "order.scheduledStart",
            "order.scheduledEnd", "asset.name", "asset.code", "recipient.name"
        },
        CommunicationEntityScope.WorkTask => new[]
        {
            "task.id", "task.title", "task.state", "task.type", "task.priority",
            "task.dueAt", "asset.name", "asset.code", "recipient.name"
        },
        CommunicationEntityScope.Asset => new[]
        {
            "asset.name", "asset.code", "asset.state", "asset.toState", "recipient.name"
        },
        _ => Array.Empty<string>()
    };
}
