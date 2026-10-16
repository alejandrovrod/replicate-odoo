using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Tax.Queries;

/// <summary>Active templates of one company and category (the UI selector source).</summary>
public sealed record GetTaxTemplatesQuery(Guid CompanyId, string Category)
    : IQuery<IReadOnlyList<TaxTemplateDto>>;

/// <summary>One template with its lines (or null).</summary>
public sealed record GetTaxTemplateByIdQuery(Guid CompanyId, Guid Id)
    : IQuery<TaxTemplateDto?>;

public sealed class GetTaxTemplatesQueryHandler
    : IQueryHandler<GetTaxTemplatesQuery, IReadOnlyList<TaxTemplateDto>>
{
    private readonly ITaxTemplateRepository _templates;

    public GetTaxTemplatesQueryHandler(ITaxTemplateRepository templates)
    {
        _templates = templates;
    }

    public async Task<IReadOnlyList<TaxTemplateDto>> HandleAsync(
        GetTaxTemplatesQuery query, CancellationToken cancellationToken = default)
    {
        var rows = await _templates.GetByCompanyCategoryAsync(
            query.CompanyId, (query.Category ?? string.Empty).Trim(), cancellationToken);
        return rows
            .Where(t => t.CompanyId == query.CompanyId)
            .OrderByDescending(t => t.IsDefault)
            .ThenBy(t => t.Title, StringComparer.Ordinal)
            .Select(TaxTemplateDto.From)
            .ToList();
    }
}

public sealed class GetTaxTemplateByIdQueryHandler
    : IQueryHandler<GetTaxTemplateByIdQuery, TaxTemplateDto?>
{
    private readonly ITaxTemplateRepository _templates;

    public GetTaxTemplateByIdQueryHandler(ITaxTemplateRepository templates)
    {
        _templates = templates;
    }

    public async Task<TaxTemplateDto?> HandleAsync(
        GetTaxTemplateByIdQuery query, CancellationToken cancellationToken = default)
    {
        var template = await _templates.GetByIdAsync(query.Id, cancellationToken);
        return template is null || template.CompanyId != query.CompanyId
            ? null
            : TaxTemplateDto.From(template);
    }
}
