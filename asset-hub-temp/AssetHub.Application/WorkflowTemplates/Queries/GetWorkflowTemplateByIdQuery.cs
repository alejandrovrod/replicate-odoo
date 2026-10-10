using System;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.WorkflowTemplates;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.WorkflowTemplates.Queries;

public record WorkflowTemplateDetailDto(
    Guid Id,
    string Code,
    string Name,
    string Description,
    string SchemaJson,
    Domain.AssetTemplates.LifecycleConfig LifecycleStates,
    bool IsActive
);

public record GetWorkflowTemplateByIdQuery(Guid Id) : IRequest<WorkflowTemplateDetailDto?>;

public class GetWorkflowTemplateByIdQueryHandler : IRequestHandler<GetWorkflowTemplateByIdQuery, WorkflowTemplateDetailDto?>
{
    private readonly ITenantDbContext _context;

    public GetWorkflowTemplateByIdQueryHandler(ITenantDbContext context)
    {
        _context = context;
    }

    public async Task<WorkflowTemplateDetailDto?> Handle(GetWorkflowTemplateByIdQuery request, CancellationToken cancellationToken)
    {
        var template = await _context.WorkflowTemplates
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

        if (template == null) return null;

        return new WorkflowTemplateDetailDto(
            template.Id,
            template.Code,
            template.Name,
            template.Description,
            template.SchemaJson,
            template.LifecycleStates,
            template.IsActive
        );
    }
}
