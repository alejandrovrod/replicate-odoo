using System;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AssetHub.Application.Analytics.Commands;

public class GenerateCostsReportCommand : IRequest<Guid>
{
    public string Email { get; set; } = string.Empty;
}

public class GenerateCostsReportCommandHandler : IRequestHandler<GenerateCostsReportCommand, Guid>
{
    private readonly ILogger<GenerateCostsReportCommandHandler> _logger;

    public GenerateCostsReportCommandHandler(ILogger<GenerateCostsReportCommandHandler> logger)
    {
        _logger = logger;
    }

    public Task<Guid> Handle(GenerateCostsReportCommand request, CancellationToken cancellationToken)
    {
        var jobId = Guid.NewGuid();
        
        // Mocking async report generation and email sending
        _logger.LogInformation("Job {JobId} accepted. Generating costs report asynchronously and will send link to {Email}", jobId, request.Email);
        
        // In a real application, we would enqueue a message to a message broker (e.g. RabbitMQ, Azure Service Bus) 
        // or a background job scheduler (e.g. Hangfire) here.
        Task.Run(async () =>
        {
            await Task.Delay(2000); // Simulate work
            _logger.LogInformation("Job {JobId} completed. Report sent to {Email}", jobId, request.Email);
        });

        return Task.FromResult(jobId);
    }
}
