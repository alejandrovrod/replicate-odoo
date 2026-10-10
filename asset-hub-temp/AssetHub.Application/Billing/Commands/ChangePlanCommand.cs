using System;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Exceptions;
using MediatR;

namespace AssetHub.Application.Billing.Commands;

public record ChangePlanCommand(string PlanCode) : IRequest<bool>;

public class ChangePlanCommandHandler : IRequestHandler<ChangePlanCommand, bool>
{
    private readonly IUsageTracker _usageTracker;

    public ChangePlanCommandHandler(IUsageTracker usageTracker)
    {
        _usageTracker = usageTracker;
    }

    public async Task<bool> Handle(ChangePlanCommand request, CancellationToken cancellationToken)
    {
        // TODO: Buscar el plan solicitado
        // TODO: Validar que el uso actual (_usageTracker) no supere los límites del nuevo plan
        // var currentUsers = await _usageTracker.GetCurrentUsersCountAsync();
        // if (currentUsers > newPlan.MaxUsers) throw new PlanLimitExceededException("Users", "Debe eliminar usuarios antes de bajar de plan.");

        // TODO: Llamar al IBillingProvider para ejecutar el cambio (prorrateo)
        
        return true;
    }
}
