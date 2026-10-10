using System;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.WorkflowTemplates.Commands;

public record DeleteWorkflowTemplateCommand(Guid Id) : IRequest;

public class DeleteWorkflowTemplateCommandHandler : IRequestHandler<DeleteWorkflowTemplateCommand>
{
    private readonly ITenantDbContext _context;

    public DeleteWorkflowTemplateCommandHandler(ITenantDbContext context)
    {
        _context = context;
    }

    public async Task Handle(DeleteWorkflowTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _context.WorkflowTemplates.FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);
        if (template != null)
        {
            template.IsActive = false; // Soft delete
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
