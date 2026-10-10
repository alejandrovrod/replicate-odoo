using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.CommunicationTemplates.Rendering;
using AssetHub.Application.Interfaces;
using AssetHub.Application.Incidents.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AssetHub.Application.Incidents.EventHandlers;

public class IncidentReportedEventHandler : INotificationHandler<IncidentReportedEvent>
{
    private const string TemplateCode = "INCIDENT-REPORTED";

    private readonly ITenantDbContext _db;
    private readonly IEmailService _emailService;
    private readonly ICommunicationTemplateService _templateService;
    private readonly ILogger<IncidentReportedEventHandler> _logger;

    public IncidentReportedEventHandler(
        ITenantDbContext db,
        IEmailService emailService,
        ICommunicationTemplateService templateService,
        ILogger<IncidentReportedEventHandler> logger)
    {
        _db = db;
        _emailService = emailService;
        _templateService = templateService;
        _logger = logger;
    }

    public async Task Handle(IncidentReportedEvent notification, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(notification.PropertiesJson) || notification.PropertiesJson == "{}")
        {
            return;
        }

        try
        {
            var propsDoc = JsonDocument.Parse(notification.PropertiesJson);

            // Revisa si existe "reportar_a" o "recibe_a"
            string? targetFieldId = null;
            if (propsDoc.RootElement.TryGetProperty("reportar_a", out _))
            {
                targetFieldId = "reportar_a";
            }
            else if (propsDoc.RootElement.TryGetProperty("recibe_a", out _))
            {
                targetFieldId = "recibe_a";
            }

            if (targetFieldId == null) return;
            if (!propsDoc.RootElement.TryGetProperty(targetFieldId, out var targetValueElement)) return;

            var targetValueStr = targetValueElement.GetString();
            if (!Guid.TryParse(targetValueStr, out var targetEmployeeId))
            {
                _logger.LogWarning("=> El valor del campo destinatario {TargetFieldId} no es un GUID válido (Incidencia).", targetFieldId);
                return;
            }

            var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == targetEmployeeId, cancellationToken);
            if (employee == null || string.IsNullOrEmpty(employee.Email))
            {
                _logger.LogWarning("=> No se encontró el empleado con ID {EmployeeId} o no tiene un email configurado (Incidencia).", targetEmployeeId);
                return;
            }

            var incident = await _db.Incidents
                .Include(i => i.Asset)
                .FirstOrDefaultAsync(i => i.Id == notification.IncidentId, cancellationToken);
                
            var variables = new System.Collections.Generic.Dictionary<string, object>
            {
                { "EmployeeName", $"{employee.FirstName} {employee.LastName}" },
                { "IncidentTitle", notification.Title }
            };

            if (incident != null)
            {
                variables["incident"] = new 
                {
                    title = incident.Title,
                    state = incident.State
                };
                
                if (incident.Asset != null)
                {
                    variables["asset"] = new { name = incident.Asset.Name };
                }
            }

            // Lookup Notification Mapping for "Incident.Created"
            var mapping = await _db.NotificationMappings
                .FirstOrDefaultAsync(m => m.SystemEvent == "Incident.Created" && m.IsActive, cancellationToken);

            RenderedCommunication? rendered = null;

            if (mapping != null)
            {
                rendered = await _templateService.RenderByIdAsync(
                    notification.TenantId, mapping.TemplateId, employee.PreferredLocale, variables, cancellationToken);
            }
            else
            {
                // Fallback to old behavior if no mapping
                rendered = await _templateService.RenderActiveAsync(
                    notification.TenantId, "INCIDENT-REPORTED", employee.PreferredLocale, variables, cancellationToken);
            }

            string subject;
            string body;
            if (rendered != null)
            {
                subject = rendered.Subject ?? $"Notificación: Nueva Incidencia Reportada";
                body = rendered.Body;
            }
            else
            {
                subject = $"Notificación: Nueva Incidencia Reportada ({notification.Title})";
                body = $"Se ha reportado una nueva incidencia: '{notification.Title}'.\nPor favor revise el sistema para más detalles.";
            }

            await _emailService.SendEmailAsync(employee.Email, subject, body, isHtml: true, cancellationToken: cancellationToken);
            _logger.LogInformation("=> Correo de nueva incidencia enviado exitosamente a {Email}", employee.Email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando el envío de correo para nueva incidencia");
        }
    }
}
