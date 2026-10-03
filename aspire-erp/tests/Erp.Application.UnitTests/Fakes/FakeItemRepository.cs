using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.UnitTests.Fakes;

/// <summary>
/// In-memory <see cref="IItemRepository"/>: tests seed the item catalog and the duplicate-SKU
/// answer per test (Task 3.1 acceptance: unique SKU per tenant).
/// </summary>
public sealed class FakeItemRepository : IItemRepository
{
    private readonly List<Item> _items = new();

    /// <summary>Items visible to the service under test.</summary>
    public IReadOnlyList<Item> Items => _items;

    /// <summary>Last item passed to AddAsync (null when nothing was created).</summary>
    public Item? AddedItem { get; private set; }

    /// <summary>When true, ExistsSkuAsync reports the code as already taken.</summary>
    public bool SkuExists { get; set; }

    public void Seed(params Item[] items) => _items.AddRange(items);

    public Task AddAsync(Item item, CancellationToken cancellationToken = default)
    {
        AddedItem = item;
        _items.Add(item);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsSkuAsync(string code, CancellationToken cancellationToken = default)
        => Task.FromResult(SkuExists || _items.Any(i => i.ItemCode == code));

    public Task<IReadOnlyList<Item>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Item>>(_items);

    public Task<Item?> GetByIdAsync(Guid itemId, CancellationToken cancellationToken = default)
        => Task.FromResult(_items.FirstOrDefault(i => i.Id == itemId));

    public Task<IReadOnlyList<Item>> GetByIdsAsync(IReadOnlyList<Guid> itemIds, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Item>>(_items.Where(i => itemIds.Contains(i.Id)).ToList());
}



