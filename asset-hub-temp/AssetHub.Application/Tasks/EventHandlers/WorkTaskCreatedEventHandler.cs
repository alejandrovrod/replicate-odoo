using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.CommunicationTemplates.Rendering;
using AssetHub.Application.Interfaces;
using AssetHub.Application.Tasks.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AssetHub.Application.Tasks.EventHandlers;

public class WorkTaskCreatedEventHandler : INotificationHandler<WorkTaskCreatedEvent>
{
    private const string TemplateCode = "TASK-CREATED";

    private readonly ITenantDbContext _db;
    private readonly IEmailService _emailService;
    private readonly ICommunicationTemplateService _templateService;
    private readonly TemplateVariableBuilder _variableBuilder;
    private readonly ILogger<WorkTaskCreatedEventHandler> _logger;

    public WorkTaskCreatedEventHandler(
        ITenantDbContext db,
        IEmailService emailService,
        ICommunicationTemplateService templateService,
        TemplateVariableBuilder variableBuilder,
        ILogger<WorkTaskCreatedEventHandler> logger)
    {
        _db = db;
        _emailService = emailService;
        _templateService = templateService;
        _variableBuilder = variableBuilder;
        _logger = logger;
    }

    public async Task Handle(WorkTaskCreatedEvent notification, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(notification.PropertiesJson) || notification.PropertiesJson == "{}")
        {
            return;
        }

        try
        {
            var propsDoc = JsonDocument.Parse(notification.PropertiesJson);

            // Revisa si existe "reportar_a" o "recibe_a"
            string targetFieldId = null;
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
                _logger.LogWarning("=> El valor del campo destinatario {TargetFieldId} no es un GUID válido (Tarea).", targetFieldId);
                return;
            }

            var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == targetEmployeeId, cancellationToken);
            if (employee == null || string.IsNullOrEmpty(employee.Email))
            {
                _logger.LogWarning("=> No se encontró el empleado con ID {EmployeeId} o no tiene un email configurado (Tarea).", targetEmployeeId);
                return;
            }

            // Buscar plantilla activa del tenant (REQ-003); si no hay, fallback hardcodeado
            var variables = await _variableBuilder.ForWorkTaskAsync(notification.TaskId, employee.Id, cancellationToken);
            
            // Lookup Notification Mapping for "Task.Created"
            var mapping = await _db.NotificationMappings
                .FirstOrDefaultAsync(m => m.SystemEvent == "Task.Created" && m.IsActive, cancellationToken);

            RenderedCommunication rendered = null;

            if (mapping != null)
            {
                rendered = await _templateService.RenderByIdAsync(
                    notification.TenantId, mapping.TemplateId, employee.PreferredLocale, variables, cancellationToken);
            }
            else
            {
                rendered = await _templateService.RenderActiveAsync(
                    notification.TenantId, TemplateCode, employee.PreferredLocale, variables, cancellationToken);
            }

            string subject;
            string body;
            if (rendered != null)
            {
                subject = rendered.Subject ?? $"Notificación: Nueva Tarea de Trabajo";
                body = rendered.Body;
            }
            else
            {
                var task = await _db.WorkTasks
                    .FirstOrDefaultAsync(t => t.Id == notification.TaskId, cancellationToken);
                var taskTitle = task?.Title ?? "Nueva Tarea";
                subject = $"Notificación: Nueva Tarea de Trabajo ({taskTitle})";
                body = $"Se ha generado una nueva Tarea de Trabajo: '{taskTitle}'.\nPor favor revise el sistema para más detalles.";
            }

            await _emailService.SendEmailAsync(employee.Email, subject, body, isHtml: true, cancellationToken: cancellationToken);
            _logger.LogInformation("=> Correo de nueva tarea enviado exitosamente a {Email}", employee.Email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando el envío de correo para nueva tarea");
        }
    }
}
