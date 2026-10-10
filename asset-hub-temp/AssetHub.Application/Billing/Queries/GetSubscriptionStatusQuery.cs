using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using MediatR;

namespace AssetHub.Application.Billing.Queries;

public record GetSubscriptionStatusQuery() : IRequest<SubscriptionStatusDto>;

public record SubscriptionStatusDto(
    string PlanName,
    int MaxUsers,
    int CurrentUsers,
    int MaxAssets,
    int CurrentAssets,
    string Status
);

public class GetSubscriptionStatusQueryHandler : IRequestHandler<GetSubscriptionStatusQuery, SubscriptionStatusDto>
{
    private readonly IUsageTracker _usageTracker;

    public GetSubscriptionStatusQueryHandler(IUsageTracker usageTracker)
    {
        _usageTracker = usageTracker;
    }

    public async Task<SubscriptionStatusDto> Handle(GetSubscriptionStatusQuery request, CancellationToken cancellationToken)
    {
        var users = await _usageTracker.GetCurrentUsersCountAsync();
        var assets = await _usageTracker.GetCurrentAssetsCountAsync();

        // TODO: Leer Plan y Subscription reales desde la base de datos para el current tenant
        
        return new SubscriptionStatusDto(
            PlanName: "Pro Plan",
            MaxUsers: 10,
            CurrentUsers: users,
            MaxAssets: 500,
            CurrentAssets: assets,
            Status: "active"
        );
    }
}
