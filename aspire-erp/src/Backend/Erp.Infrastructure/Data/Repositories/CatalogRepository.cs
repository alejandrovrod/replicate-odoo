using Erp.Domain.Entities.System;
using Erp.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Data.Repositories;

public class CatalogRepository : ICatalogRepository
{
    private readonly AppDbContext _context;

    public CatalogRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Catalog?> GetCatalogByCodeAsync(string code, string languageCode, CancellationToken cancellationToken)
    {
        return await _context.Catalogs
            .AsNoTracking()
            .Include(c => c.Items)
            .ThenInclude(i => i.Translations.Where(t => t.LanguageCode == languageCode))
            .FirstOrDefaultAsync(c => c.Code == code, cancellationToken);
    }

    public void AddCatalog(Catalog catalog)
    {
        _context.Catalogs.Add(catalog);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
