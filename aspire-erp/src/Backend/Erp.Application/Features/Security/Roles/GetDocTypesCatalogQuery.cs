using Erp.Application.Common;

namespace Erp.Application.Features.Security.Roles;

/// <summary>Static official DocType catalog (spec §5) for the matrix editor. Infallible.</summary>
public sealed record GetDocTypesCatalogQuery : IQuery<IReadOnlyList<DocTypeCatalogEntry>>;

public sealed class GetDocTypesCatalogQueryHandler : IQueryHandler<GetDocTypesCatalogQuery, IReadOnlyList<DocTypeCatalogEntry>>
{
    public Task<IReadOnlyList<DocTypeCatalogEntry>> HandleAsync(GetDocTypesCatalogQuery query, CancellationToken cancellationToken = default) =>
        Task.FromResult(DocTypeCatalog.All);
}
