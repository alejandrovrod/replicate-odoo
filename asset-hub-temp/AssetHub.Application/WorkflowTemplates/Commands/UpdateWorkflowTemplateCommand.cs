using System;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.WorkflowTemplates;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.WorkflowTemplates.Commands;

public record UpdateWorkflowTemplateCommand(
    Guid Id,
    string Code,
    string Name,
    string Description,
    string SchemaJson,
    string Type,
    Domain.AssetTemplates.LifecycleConfig LifecycleStates
) : IRequest;

public class UpdateWorkflowTemplateCommandHandler : IRequestHandler<UpdateWorkflowTemplateCommand>
{
    private readonly ITenantDbContext _context;

    public UpdateWorkflowTemplateCommandHandler(ITenantDbContext context)
    {
        _context = context;
    }

    public async Task Handle(UpdateWorkflowTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _context.WorkflowTemplates.FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);
        
        if (template == null)
            throw new Exception("Template not found");

        template.Code = request.Code;
        template.Name = request.Name;
        template.Description = request.Description;
        template.SchemaJson = string.IsNullOrWhiteSpace(request.SchemaJson) ? "{}" : request.SchemaJson;
        if (!string.IsNullOrWhiteSpace(request.Type))
            template.Type = request.Type;
        template.LifecycleStates = request.LifecycleStates ?? new();
        template.Version++;

        await _context.SaveChangesAsync(cancellationToken);
    }
}
