using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Assets.Events;
using AssetHub.Application.CommunicationTemplates.Rendering;
using AssetHub.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AssetHub.Application.Assets.EventHandlers;

public class AssetOnEnterTriggerHandler : INotificationHandler<AssetStateChangedEvent>
{
    private const string TemplateCode = "ASSET-STATE-CHANGED";

    private readonly ITenantDbContext _db;
    private readonly ILogger<AssetOnEnterTriggerHandler> _logger;
    private readonly IEmailService _emailService;
    private readonly ICommunicationTemplateService _templateService;
    private readonly TemplateVariableBuilder _variableBuilder;

    public AssetOnEnterTriggerHandler(
        ITenantDbContext db,
        ILogger<AssetOnEnterTriggerHandler> logger,
        IEmailService emailService,
        ICommunicationTemplateService templateService,
        TemplateVariableBuilder variableBuilder)
    {
        _db = db;
        _logger = logger;
        _emailService = emailService;
        _templateService = templateService;
        _variableBuilder = variableBuilder;
    }

    public async Task Handle(AssetStateChangedEvent notification, CancellationToken cancellationToken)
    {
        var asset = await _db.Assets
            .Include(a => a.AssetTemplate)
            .FirstOrDefaultAsync(a => a.Id == notification.AssetId, cancellationToken);

        if (asset == null || asset.AssetTemplate == null)
            return;

        var config = asset.AssetTemplate.LifecycleStates;
        if (config == null || config.States == null) return;

        if (config.States.TryGetValue(notification.ToState, out var stateConfig))
        {
            if (!string.IsNullOrWhiteSpace(stateConfig.OnEnterAction))
            {
                // DISPARADOR DE ACCIONES ON ENTER
                // En una aplicación real, aquí llamaríamos a un bus de mensajes (RabbitMQ, ServiceBus)
                // o usaríamos un Patrón Outbox para asegurar que la acción se ejecute.
                
                _logger.LogInformation("================================================");
                _logger.LogInformation("⚡ TRIGGER ON_ENTER DETECTADO PARA ESTADO: {State}", notification.ToState);
                _logger.LogInformation("⚡ ACCIÓN A EJECUTAR: {Action}", stateConfig.OnEnterAction);
                _logger.LogInformation("⚡ ACTIVO AFECTADO: {AssetId}", notification.AssetId);
                _logger.LogInformation("================================================");
                
                // Simulación de Dispatcher
                switch (stateConfig.OnEnterAction)
                {
                    case "CREATE_WORK_ORDER":
                        _logger.LogInformation("=> Ejecutando Side Effect: Creando Orden de Trabajo automáticamente...");
                        break;
                    case "NOTIFY_MANAGER":
                        _logger.LogInformation("=> Ejecutando Side Effect: Enviando email al supervisor...");
                        
                        var targetFieldId = stateConfig.NotificationTargetFieldId;
                        if (string.IsNullOrEmpty(targetFieldId))
                        {
                            _logger.LogWarning("No se configuró el campo destinatario para la acción NOTIFY_MANAGER en el estado {State}", notification.ToState);
                            break;
                        }

                        if (string.IsNullOrEmpty(asset.PropertiesJson))
                        {
                            _logger.LogWarning("El activo {AssetId} no tiene propiedades JSON, no se puede obtener el destinatario.", asset.Id);
                            break;
                        }

                        try
                        {
                            var propsDoc = JsonDocument.Parse(asset.PropertiesJson);
                            if (propsDoc.RootElement.TryGetProperty(targetFieldId, out var targetValueElement))
                            {
                                var targetValueStr = targetValueElement.GetString();
                                if (Guid.TryParse(targetValueStr, out var targetEmployeeId))
                                {
                                    var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == targetEmployeeId, cancellationToken);
                                    if (employee != null && !string.IsNullOrEmpty(employee.Email))
                                    {
                                        // Buscar plantilla activa del tenant; si no hay, fallback hardcodeado
                                        var tenantId = asset.TenantId;
                                        var variables = await _variableBuilder.ForAssetAsync(asset.Id, notification.ToState, employee.Id, cancellationToken);
                                        
                                        // Lookup Notification Mapping for "Asset.StateChanged"
                                        var mapping = await _db.NotificationMappings
                                            .FirstOrDefaultAsync(m => m.SystemEvent == "Asset.StateChanged" && m.IsActive, cancellationToken);

                                        RenderedCommunication rendered = null;

                                        if (mapping != null)
                                        {
                                            rendered = await _templateService.RenderByIdAsync(
                                                tenantId, mapping.TemplateId, employee.PreferredLocale, variables, cancellationToken);
                                        }
                                        else
                                        {
                                            rendered = await _templateService.RenderActiveAsync(
                                                tenantId, TemplateCode, employee.PreferredLocale, variables, cancellationToken);
                                        }

                                        string subject;
                                        string body;
                                        if (rendered != null)
                                        {
                                            subject = rendered.Subject ?? $"Notificación de Cambio de Estado: Activo {asset.Code}";
                                            body = rendered.Body;
                                        }
                                        else
                                        {
                                            subject = $"Notificación de Cambio de Estado: Activo {asset.Code}";
                                            body = $"El activo '{asset.Name}' ha transicionado al estado '{notification.ToState}'.\nPor favor revise el sistema para más detalles.";
                                        }

                                        await _emailService.SendEmailAsync(employee.Email, subject, body, isHtml: true, cancellationToken: cancellationToken);
                                        _logger.LogInformation("=> Correo enviado exitosamente a {Email}", employee.Email);
                                    }
                                    else
                                    {
                                        _logger.LogWarning("=> No se encontró el empleado con ID {EmployeeId} o no tiene un email configurado.", targetEmployeeId);
                                    }
                                }
                                else
                                {
                                    _logger.LogWarning("=> El valor del campo destinatario {TargetFieldId} no es un GUID válido.", targetFieldId);
                                }
                            }
                            else
                            {
                                _logger.LogWarning("=> El campo destinatario {TargetFieldId} no existe en las propiedades del activo.", targetFieldId);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error procesando el envío de correo para NOTIFY_MANAGER");
                        }
                        break;
                    default:
                        _logger.LogInformation("=> Ejecutando Side Effect Genérico: {Action}", stateConfig.OnEnterAction);
                        break;
                }
            }
        }
    }
}
