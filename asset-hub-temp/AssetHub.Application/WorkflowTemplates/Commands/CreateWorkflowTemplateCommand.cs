using System;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.WorkflowTemplates;
using MediatR;

namespace AssetHub.Application.WorkflowTemplates.Commands;

public record CreateWorkflowTemplateCommand(
    string Code,
    string Name,
    string Description,
    string SchemaJson,
    string Type,
    Domain.AssetTemplates.LifecycleConfig LifecycleStates
) : IRequest<Guid>;

public class CreateWorkflowTemplateCommandHandler : IRequestHandler<CreateWorkflowTemplateCommand, Guid>
{
    private readonly ITenantDbContext _context;
    private readonly ITenantResolver _tenantResolver;

    public CreateWorkflowTemplateCommandHandler(ITenantDbContext context, ITenantResolver tenantResolver)
    {
        _context = context;
        _tenantResolver = tenantResolver;
    }

    public async Task<Guid> Handle(CreateWorkflowTemplateCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId() ?? throw new InvalidOperationException("Tenant is required");

        var template = new WorkflowTemplate
        {
            TenantId = tenantId,
            Code = request.Code,
            Name = request.Name,
            Description = request.Description,
            SchemaJson = string.IsNullOrWhiteSpace(request.SchemaJson) ? "{}" : request.SchemaJson,
            Type = string.IsNullOrWhiteSpace(request.Type) ? "incident" : request.Type,
            LifecycleStates = request.LifecycleStates ?? new()
        };

        _context.WorkflowTemplates.Add(template);
        await _context.SaveChangesAsync(cancellationToken);

        return template.Id;
    }
}
