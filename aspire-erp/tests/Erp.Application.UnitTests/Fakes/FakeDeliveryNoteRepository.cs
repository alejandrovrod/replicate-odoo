using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.UnitTests.Fakes;

/// <summary>
/// In-memory <see cref="IDeliveryNoteRepository"/>: gapless DN numbers come from a per-(company, year)
/// sequence, the posting transaction simply executes its callback, and every persisted note is
/// captured so tests can assert on DeliveryNote rows without a database.
/// </summary>
public sealed class FakeDeliveryNoteRepository : IDeliveryNoteRepository
{
    private readonly List<DeliveryNote> _notes = new();
    private readonly Dictionary<(Guid CompanyId, int Year), int> _sequences = new();

    public IReadOnlyList<DeliveryNote> Notes => _notes;

    /// <summary>Number of transactions opened (proves the posting runs inside ONE transaction).</summary>
    public int TransactionCount { get; private set; }

    public Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        TransactionCount++;
        return operation(cancellationToken);
    }

    public Task<string> NextDeliveryVoucherNumberAsync(
        Guid companyId, int year, CancellationToken cancellationToken = default)
    {
        var key = (companyId, year);
        _sequences.TryGetValue(key, out var current);
        _sequences[key] = current + 1;
        return Task.FromResult($"DN-{year}-{current + 1:D5}");
    }

    public Task AddDeliveryNoteAsync(DeliveryNote deliveryNote, CancellationToken cancellationToken = default)
    {
        _notes.Add(deliveryNote);
        return Task.CompletedTask;
    }

    public Task<DeliveryNote?> GetDeliveryNoteByIdAsync(
        Guid deliveryNoteId, CancellationToken cancellationToken = default)
        => Task.FromResult(_notes.FirstOrDefault(n => n.Id == deliveryNoteId));

    // In-memory fakes ignore paging and return the whole seeded set (see FakeCustomerRepository).
    public Task<PagedResult<DeliveryNote>> GetRecentDeliveryNotesByCompanyAsync(
        Guid companyId, PagedRequest paging, CancellationToken cancellationToken = default)
    {
        var items = _notes.Where(n => n.CompanyId == companyId).ToList();
        return Task.FromResult(new PagedResult<DeliveryNote>(items, items.Count, paging.SafePageNumber, paging.SafePageSize));
    }
}
