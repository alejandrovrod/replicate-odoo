using Erp.Domain.Entities;
using Erp.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ISerialNoRepository"/> (batch-and-serial Fase 3).
/// No manual tenant predicates (Constitution II.3 - the global query filter scopes reads).
/// </summary>
public sealed class SerialNoRepository : ISerialNoRepository
{
    private readonly AppDbContext _dbContext;

    public SerialNoRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<SerialNo>> GetByNumbersAsync(
        IReadOnlyCollection<string> serialNumbers, CancellationToken cancellationToken = default)
    {
        if (serialNumbers.Count == 0)
        {
            return Array.Empty<SerialNo>();
        }

        return await _dbContext.SerialNos
            .Where(s => serialNumbers.Contains(s.SerialNumber))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SerialNo>> GetByStockEntryItemIdsAsync(
        IReadOnlyCollection<Guid> stockEntryItemIds, CancellationToken cancellationToken = default)
    {
        if (stockEntryItemIds.Count == 0)
        {
            return Array.Empty<SerialNo>();
        }

        return await _dbContext.SerialNos
            .Where(s => s.StockEntryItemId.HasValue && stockEntryItemIds.Contains(s.StockEntryItemId.Value))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SerialNo>> GetByPurchaseDocumentNoAsync(
        string purchaseDocumentNo, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(purchaseDocumentNo))
        {
            return Array.Empty<SerialNo>();
        }

        return await _dbContext.SerialNos
            .Where(s => s.PurchaseDocumentNo == purchaseDocumentNo)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SerialNo>> GetActiveByItemWarehouseAsync(
        Guid itemId, Guid warehouseId, CancellationToken cancellationToken = default)
        => await _dbContext.SerialNos
            .Where(s => s.ItemId == itemId
                && s.CurrentWarehouseId == warehouseId
                && s.Status == SerialNoStatus.Active)
            .OrderBy(s => s.SerialNumber)
            .ToListAsync(cancellationToken);

    public async Task AddRangeAsync(IReadOnlyList<SerialNo> serials, CancellationToken cancellationToken = default)
    {
        if (serials.Count == 0)
        {
            return;
        }

        await _dbContext.SerialNos.AddRangeAsync(serials, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateRangeAsync(IReadOnlyList<SerialNo> serials, CancellationToken cancellationToken = default)
    {
        if (serials.Count == 0)
        {
            return;
        }

        _dbContext.SerialNos.UpdateRange(serials);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteRangeAsync(IReadOnlyList<SerialNo> serials, CancellationToken cancellationToken = default)
    {
        if (serials.Count == 0)
        {
            return;
        }

        _dbContext.SerialNos.RemoveRange(serials);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
