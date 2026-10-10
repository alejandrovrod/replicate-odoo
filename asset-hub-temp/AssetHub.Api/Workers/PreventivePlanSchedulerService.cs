using System;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Api.Configuration;
using AssetHub.Application.Maintenance.Commands;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AssetHub.Api.Workers;

public class PreventivePlanSchedulerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PreventivePlanSchedulerService> _logger;
    private readonly int _intervalMinutes;

    public PreventivePlanSchedulerService(
        IServiceProvider serviceProvider,
        ILogger<PreventivePlanSchedulerService> logger,
        IOptions<SchedulerSettings> schedulerSettings)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _intervalMinutes = schedulerSettings.Value.IntervalMinutes > 0 ? schedulerSettings.Value.IntervalMinutes : 1;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PreventivePlanSchedulerService is starting. Interval: {IntervalMinutes} minutes.", _intervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("PreventivePlanSchedulerService is evaluating plans at {Time}.", DateTimeOffset.UtcNow);

                using (var scope = _serviceProvider.CreateScope())
                {
                    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                    
                    // Dispara la evaluación de planes en todos los tenants
                    await mediator.Send(new EvaluatePreventivePlansCommand(), stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while evaluating preventive plans in background service.");
            }

            await Task.Delay(TimeSpan.FromMinutes(_intervalMinutes), stoppingToken);
        }

        _logger.LogInformation("PreventivePlanSchedulerService is stopping.");
    }
}
