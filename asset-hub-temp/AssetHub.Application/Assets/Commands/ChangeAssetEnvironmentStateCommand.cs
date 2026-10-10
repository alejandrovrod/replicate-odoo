using System;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Nodes;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Assets;
using AssetHub.Domain.AssetTemplates;
using AssetHub.Application.Assets.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AssetHub.Application.Assets.Commands;

public record ChangeAssetEnvironmentStateCommand(
    Guid AssetId,
    string ToState,
    string? Notes,
    System.Collections.Generic.Dictionary<string, JsonElement>? TransitionData = null,
    bool IsAutomatedTransition = false) : IRequest<bool>;

public class ChangeAssetEnvironmentStateCommandHandler : IRequestHandler<ChangeAssetEnvironmentStateCommand, bool>
{
    private readonly ITenantDbContext _dbContext;
    private readonly IMediator _mediator;
    private readonly ILogger<ChangeAssetEnvironmentStateCommandHandler> _logger;

    public ChangeAssetEnvironmentStateCommandHandler(ITenantDbContext dbContext, IMediator mediator, ILogger<ChangeAssetEnvironmentStateCommandHandler> logger)
    {
        _dbContext = dbContext;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<bool> Handle(ChangeAssetEnvironmentStateCommand request, CancellationToken cancellationToken)
    {
        var asset = await _dbContext.Assets
            .Include(a => a.AssetTemplate)
            .FirstOrDefaultAsync(a => a.Id == request.AssetId, cancellationToken);
            
        if (asset == null) return false;

        var fromState = asset.State;
        
        // Validación del template
        if (asset.AssetTemplate != null)
        {
            var allowedTransitions = asset.AssetTemplate.LifecycleStates.Transitions;
            if (!allowedTransitions.ContainsKey(fromState) || !allowedTransitions[fromState].Contains(request.ToState))
            {
                throw new InvalidOperationException($"Transición no permitida de {fromState} a {request.ToState}");
            }

            var statesConfig = asset.AssetTemplate.LifecycleStates.States;
            
            if (statesConfig != null)
            {
                // Ver si el estado actual es terminal
                if (statesConfig.TryGetValue(fromState, out var currentConfig) && currentConfig.IsTerminal)
                {
                    throw new InvalidOperationException($"El estado actual {fromState} es terminal. No se permiten más transiciones.");
                }

                if (statesConfig.TryGetValue(request.ToState, out var targetConfig))
                {
                    // Validación de Roles (mock)
                    if (targetConfig.AllowedRoles != null && targetConfig.AllowedRoles.Count > 0)
                    {
                        // En la vida real verificaríamos el rol del usuario conectado
                        // if (!targetConfig.AllowedRoles.Intersect(userRoles).Any()) throw...
                    }

                    // Validación de Campos Requeridos
                    // Las transiciones automáticas (propagación de estados, bloqueo por incidencia)
                    // no tienen datos de formulario del usuario, así que omitimos esta validación.
                    if (!request.IsAutomatedTransition && targetConfig.RequiresFields != null && targetConfig.RequiresFields.Count > 0)
                    {
                        if (request.TransitionData == null)
                        {
                            _logger.LogWarning("Validación falló: TransitionData es null pero se requieren campos: {Fields}", string.Join(", ", targetConfig.RequiresFields));
                            throw new ArgumentException("Faltan campos requeridos para cambiar de estado.");
                        }

                        _logger.LogInformation("Validando campos requeridos para transición a {ToState}. Campos esperados: {ExpectedFields}. Datos recibidos: {ReceivedKeys}", 
                            request.ToState, 
                            string.Join(", ", targetConfig.RequiresFields), 
                            string.Join(", ", request.TransitionData.Keys));

                        foreach (var field in targetConfig.RequiresFields)
                        {
                            if (!request.TransitionData.ContainsKey(field))
                            {
                                // We allow missing fields because they might be conditionally hidden by JSON schema dependencies.
                                // The frontend RJSF validation ensures that active required fields are always sent.
                                continue;
                            }

                            var element = request.TransitionData[field];
                            _logger.LogInformation("Campo '{Field}' recibido con ValueKind: {ValueKind}, valor: {Value}", field, element.ValueKind, element.ToString());

                            if (element.ValueKind == JsonValueKind.Null || 
                                element.ValueKind == JsonValueKind.Undefined ||
                                (element.ValueKind == JsonValueKind.Array && element.GetArrayLength() == 0) ||
                                (element.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(element.GetString())))
                            {
                                _logger.LogWarning("Validación falló: El campo '{Field}' está vacío o es null.", field);
                                throw new ArgumentException($"El campo '{field}' es requerido para cambiar al estado {request.ToState}");
                            }
                        }
                    }

                    // Validación de consistencia con hijos directos.
                    // Si el estado destino tiene reglas que lo sacarían automáticamente porque
                    // un hijo está en mal estado, no permitimos el cambio manual.
                    if (!request.IsAutomatedTransition)
                    {
                        await ValidateChildStateConsistencyAsync(asset, request.ToState, targetConfig, cancellationToken);
                    }

                    // Trigger Acciones Automáticas (mock logs)
                    if (!string.IsNullOrEmpty(targetConfig.OnEnterAction))
                    {
                        // Log the action execution
                        Console.WriteLine($"[AUTOMATED ACTION TRIGGERED] Asset: {asset.Id}, Action: {targetConfig.OnEnterAction}");
                    }
                }
            }
        }

        // Merge TransitionData into PropertiesJson
        if (request.TransitionData != null && request.TransitionData.Count > 0)
        {
            var currentProps = string.IsNullOrWhiteSpace(asset.PropertiesJson) 
                ? new JsonObject() 
                : JsonNode.Parse(asset.PropertiesJson)?.AsObject() ?? new JsonObject();
            
            foreach (var kvp in request.TransitionData)
            {
                currentProps[kvp.Key] = JsonNode.Parse(kvp.Value.GetRawText());
            }
            asset.PropertiesJson = currentProps.ToJsonString();
        }

        asset.State = request.ToState;

        var serializedTransitionData = request.TransitionData != null && request.TransitionData.Count > 0 
            ? JsonSerializer.Serialize(request.TransitionData) 
            : null;

        _dbContext.AssetLifecycleEvents.Add(new AssetLifecycleEvent
        {
            AssetId = asset.Id,
            EventType = "cambio_estado",
            FromState = fromState,
            ToState = request.ToState,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? serializedTransitionData : request.Notes + (serializedTransitionData != null ? "\nData: " + serializedTransitionData : ""),
            At = DateTime.UtcNow,
            UserId = Guid.Empty // Debe venir de Auth
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        
        // Publish Event for background processors (Notifications, Modules, etc)
        await _mediator.Publish(new AssetStateChangedEvent(
            asset.Id, 
            fromState, 
            request.ToState, 
            asset.AssetTemplate?.Name ?? "Desconocido", 
            request.TransitionData
        ), cancellationToken);

        return true;
    }

    private async Task ValidateChildStateConsistencyAsync(Asset asset, string targetState, StateConfig targetConfig, CancellationToken cancellationToken)
    {
        if (targetConfig.ChildStateDependencies == null || !targetConfig.ChildStateDependencies.Any())
        {
            return;
        }

        var children = await _dbContext.Assets
            .Where(a => a.ParentId == asset.Id && !a.IsDeleted)
            .ToListAsync(cancellationToken);

        if (!children.Any())
        {
            return;
        }

        foreach (var rule in targetConfig.ChildStateDependencies)
        {
            if (string.IsNullOrWhiteSpace(rule.TargetState) || rule.TargetState == targetState)
            {
                continue;
            }

            bool conditionMet = false;

            if (rule.ConditionType.Equals("Any", StringComparison.OrdinalIgnoreCase))
            {
                conditionMet = children.Any(c => rule.ChildStates.Contains(c.State));
            }
            else if (rule.ConditionType.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                conditionMet = children.All(c => rule.ChildStates.Contains(c.State));
            }

            if (conditionMet)
            {
                var offendingStates = children
                    .Where(c => rule.ChildStates.Contains(c.State))
                    .Select(c => $"{c.Name} ({c.State})")
                    .Distinct();

                throw new InvalidOperationException(
                    $"No se puede cambiar el activo a '{targetState}' porque tiene hijos en estados inconsistentes: {string.Join(", ", offendingStates)}. " +
                    $"Regla: cuando {rule.ConditionType.ToLowerInvariant()} hijo(s) está(n) en {string.Join(", ", rule.ChildStates)}, " +
                    $"el padre debe ir a '{rule.TargetState}'.");
            }
        }
    }
}
