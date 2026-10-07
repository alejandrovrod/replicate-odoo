using Erp.Domain.Entities.System;

namespace Erp.Domain.Repositories;

public interface ICatalogRepository
{
    Task<Catalog?> GetCatalogByCodeAsync(string code, string languageCode, CancellationToken cancellationToken);
    void AddCatalog(Catalog catalog);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
