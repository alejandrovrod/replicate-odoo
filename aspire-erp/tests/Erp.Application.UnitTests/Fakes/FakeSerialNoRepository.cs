using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.UnitTests.Fakes;

/// <summary>In-memory <see cref="ISerialNoRepository"/> over a seeded serial set.</summary>
public sealed class FakeSerialNoRepository : ISerialNoRepository
{
    private readonly List<SerialNo> _serials = new();

    public IReadOnlyList<SerialNo> Serials => _serials;

    public void Seed(params SerialNo[] serials) => _serials.AddRange(serials);

    public Task<IReadOnlyList<SerialNo>> GetByNumbersAsync(
        IReadOnlyCollection<string> serialNumbers, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<SerialNo>>(
            _serials.Where(s => serialNumbers.Contains(s.SerialNumber)).ToList());

    public Task<IReadOnlyList<SerialNo>> GetByStockEntryItemIdsAsync(
        IReadOnlyCollection<Guid> stockEntryItemIds, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<SerialNo>>(
            _serials.Where(s => s.StockEntryItemId.HasValue && stockEntryItemIds.Contains(s.StockEntryItemId.Value)).ToList());

    public Task<IReadOnlyList<SerialNo>> GetByPurchaseDocumentNoAsync(
        string purchaseDocumentNo, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<SerialNo>>(
            string.IsNullOrWhiteSpace(purchaseDocumentNo)
                ? Array.Empty<SerialNo>()
                : _serials.Where(s => s.PurchaseDocumentNo == purchaseDocumentNo).ToList());

    public Task<IReadOnlyList<SerialNo>> GetActiveByItemWarehouseAsync(
        Guid itemId, Guid warehouseId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<SerialNo>>(
            _serials
                .Where(s => s.ItemId == itemId
                    && s.CurrentWarehouseId == warehouseId
                    && s.Status == SerialNoStatus.Active)
                .OrderBy(s => s.SerialNumber)
                .ToList());

    public Task AddRangeAsync(IReadOnlyList<SerialNo> serials, CancellationToken cancellationToken = default)
    {
        _serials.AddRange(serials);
        return Task.CompletedTask;
    }

    public Task UpdateRangeAsync(IReadOnlyList<SerialNo> serials, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task DeleteRangeAsync(IReadOnlyList<SerialNo> serials, CancellationToken cancellationToken = default)
    {
        foreach (var serial in serials)
        {
            _serials.Remove(serial);
        }

        return Task.CompletedTask;
    }
}
