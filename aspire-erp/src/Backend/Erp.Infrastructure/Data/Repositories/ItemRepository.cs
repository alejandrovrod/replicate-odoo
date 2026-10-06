using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;
using Erp.Infrastructure.Data.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IItemRepository"/>. Tenant isolation is AUTOMATIC through
/// AppDbContext's global query filter - no manual <c>.Where(i =&gt; i.TenantId == ...)</c> here
/// (Constitution Article II.3), which is exactly what makes ExistsSkuAsync a PER-TENANT check for
/// Task 3.1's acceptance criterion.
/// </summary>
public sealed class ItemRepository : IItemRepository
{
    private readonly AppDbContext _dbContext;

    public ItemRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(Item item, CancellationToken cancellationToken = default)
    {
        await _dbContext.Items.AddAsync(item, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> ExistsSkuAsync(string code, CancellationToken cancellationToken = default)
        => _dbContext.Items.AnyAsync(i => i.ItemCode == code, cancellationToken);

    public async Task<PagedResult<Item>> GetAllAsync(PagedRequest paging, CancellationToken cancellationToken = default)
        => await _dbContext.Items
            .OrderBy(i => i.ItemCode)
            .ToPagedResultAsync(paging, cancellationToken);

    public Task<Item?> GetByIdAsync(Guid itemId, CancellationToken cancellationToken = default)
        => _dbContext.Items.FirstOrDefaultAsync(i => i.Id == itemId, cancellationToken);

    public async Task<IReadOnlyList<Item>> GetByIdsAsync(IReadOnlyList<Guid> itemIds, CancellationToken cancellationToken = default)
    {
        if (itemIds.Count == 0)
        {
            return Array.Empty<Item>();
        }

        return await _dbContext.Items
            .Where(i => itemIds.Contains(i.Id))
            .ToListAsync(cancellationToken);
    }
}
