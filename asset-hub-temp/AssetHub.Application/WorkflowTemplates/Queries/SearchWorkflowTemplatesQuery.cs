using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.WorkflowTemplates.Queries;

public record WorkflowTemplateSummaryDto(
    Guid Id,
    string Code,
    string Name,
    string Description,
    string Type,
    bool IsActive
);

public record SearchWorkflowTemplatesQuery(string? SearchTerm, bool IncludeInactive = false) : IRequest<List<WorkflowTemplateSummaryDto>>;

public class SearchWorkflowTemplatesQueryHandler : IRequestHandler<SearchWorkflowTemplatesQuery, List<WorkflowTemplateSummaryDto>>
{
    private readonly ITenantDbContext _context;

    public SearchWorkflowTemplatesQueryHandler(ITenantDbContext context)
    {
        _context = context;
    }

    public async Task<List<WorkflowTemplateSummaryDto>> Handle(SearchWorkflowTemplatesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.WorkflowTemplates.AsQueryable();

        if (!request.IncludeInactive)
            query = query.Where(t => t.IsActive);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.ToLower();
            query = query.Where(t => t.Name.ToLower().Contains(term) || t.Code.ToLower().Contains(term));
        }

        return await query
            .OrderBy(t => t.Name)
            .Select(t => new WorkflowTemplateSummaryDto(
                t.Id,
                t.Code,
                t.Name,
                t.Description,
                t.Type,
                t.IsActive
            ))
            .ToListAsync(cancellationToken);
    }
}
