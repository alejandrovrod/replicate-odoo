using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.CommunicationTemplates;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.CommunicationTemplates.Queries;

/// <summary>
/// Lista las plantillas del tenant con filtros opcionales por scope y tipo.
/// </summary>
public class GetCommunicationTemplatesQuery : IRequest<GetCommunicationTemplatesResult>
{
    public CommunicationEntityScope? EntityScope { get; set; }
    public CommunicationTemplateType? TemplateType { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class GetCommunicationTemplatesResult
{
    public List<CommunicationTemplateSummaryDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class CommunicationTemplateSummaryDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public CommunicationEntityScope EntityScope { get; set; }
    public CommunicationTemplateType TemplateType { get; set; }
    public Guid? ActiveVersionId { get; set; }
    public int? ActiveVersionNumber { get; set; }
    public int VersionCount { get; set; }
    public List<string> Locales { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

public class GetCommunicationTemplatesQueryHandler : IRequestHandler<GetCommunicationTemplatesQuery, GetCommunicationTemplatesResult>
{
    private readonly ITenantDbContext _db;

    public GetCommunicationTemplatesQueryHandler(ITenantDbContext db)
    {
        _db = db;
    }

    public async Task<GetCommunicationTemplatesResult> Handle(GetCommunicationTemplatesQuery request, CancellationToken cancellationToken)
    {
        var query = _db.CommunicationTemplates
            .AsNoTracking()
            .Include(t => t.Versions)
                .ThenInclude(v => v.Translations)
            .AsQueryable();

        if (request.EntityScope.HasValue)
            query = query.Where(t => t.EntityScope == request.EntityScope.Value);

        if (request.TemplateType.HasValue)
            query = query.Where(t => t.TemplateType == request.TemplateType.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLowerInvariant();
            query = query.Where(t =>
                t.Code.ToLower().Contains(term) ||
                t.Name.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(t => t.Versions.Min(v => v.CreatedAt))
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(t =>
        {
            var activeVersion = t.Versions.FirstOrDefault(v => v.Id == t.ActiveVersionId);
            return new CommunicationTemplateSummaryDto
            {
                Id = t.Id,
                Code = t.Code,
                Name = t.Name,
                EntityScope = t.EntityScope,
                TemplateType = t.TemplateType,
                ActiveVersionId = t.ActiveVersionId,
                ActiveVersionNumber = activeVersion?.VersionNumber,
                VersionCount = t.Versions.Count,
                Locales = activeVersion?.Translations.Select(tr => tr.Locale).ToList() ?? new List<string>(),
                CreatedAt = t.Versions.Min(v => v.CreatedAt)
            };
        }).ToList();

        return new GetCommunicationTemplatesResult
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
