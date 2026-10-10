using System;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Assets;
using AssetHub.Application.Assets.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Assets.EventHandlers;

public class NotifyManagerOnStateChangeHandler : INotificationHandler<AssetStateChangedEvent>
{
    private readonly ITenantDbContext _dbContext;

    public NotifyManagerOnStateChangeHandler(ITenantDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(AssetStateChangedEvent notification, CancellationToken cancellationToken)
    {
        // En una implementación real, aquí leeríamos targetConfig.OnEnterAction
        // o despacharíamos el evento a un sistema de mensajería (RabbitMQ, Kafka, Azure Service Bus).
        
        var asset = await _dbContext.Assets
            .Include(a => a.AssetTemplate)
            .FirstOrDefaultAsync(a => a.Id == notification.AssetId, cancellationToken);
            
        if (asset?.AssetTemplate?.LifecycleStates?.States == null) return;
        
        if (asset.AssetTemplate.LifecycleStates.States.TryGetValue(notification.ToState, out var targetConfig))
        {
            if (targetConfig.OnEnterAction == "NOTIFY_MANAGER")
            {
                // TODO: Usar IEmailService o SMS Service para notificar
                Console.WriteLine($"[NOTIFICATION SENT] El activo '{asset.Name}' cambió de estado a '{notification.ToState}'. Notificando al gerente...");
            }
            else if (targetConfig.OnEnterAction == "CREATE_WORK_ORDER")
            {
                Console.WriteLine($"[WORK ORDER CREATED] Orden de trabajo creada automáticamente para el activo '{asset.Name}' por entrar al estado '{notification.ToState}'.");
            }
        }
    }
}
