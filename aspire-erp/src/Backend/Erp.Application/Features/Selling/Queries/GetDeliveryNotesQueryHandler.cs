using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Selling.Queries;

/// <summary>
/// Assembles <see cref="GetDeliveryNotesQuery"/>: notes from <see cref="IDeliveryNoteRepository"/>,
/// line item codes/names from <see cref="IItemRepository"/>. Tenant isolation is automatic
/// (Constitution II.3); CompanyId is business scoping.
/// </summary>
public sealed class GetDeliveryNotesQueryHandler
    : IQueryHandler<GetDeliveryNotesQuery, IReadOnlyList<DeliveryNoteDto>>
{
    private readonly IDeliveryNoteRepository _deliveryNotes;
    private readonly IItemRepository _items;

    public GetDeliveryNotesQueryHandler(IDeliveryNoteRepository deliveryNotes, IItemRepository items)
    {
        _deliveryNotes = deliveryNotes;
        _items = items;
    }

    public async Task<IReadOnlyList<DeliveryNoteDto>> HandleAsync(
        GetDeliveryNotesQuery query,
        CancellationToken cancellationToken = default)
    {
        var limit = query.Limit <= 0 ? 50 : Math.Min(query.Limit, 500);
        var notes = await _deliveryNotes.GetRecentDeliveryNotesByCompanyAsync(query.CompanyId, limit, cancellationToken);
        if (notes.Count == 0)
        {
            return Array.Empty<DeliveryNoteDto>();
        }

        var itemById = await LoadItemsAsync(notes, cancellationToken);

        var result = new List<DeliveryNoteDto>(notes.Count);
        foreach (var note in notes)
        {
            result.Add(DeliveryNoteDto.Build(note, itemById));
        }

        return result;
    }

    private async Task<Dictionary<Guid, Item>> LoadItemsAsync(
        IReadOnlyList<DeliveryNote> notes,
        CancellationToken cancellationToken)
    {
        var itemIds = new HashSet<Guid>();
        foreach (var note in notes)
        {
            foreach (var line in note.Lines)
            {
                itemIds.Add(line.ItemId);
            }
        }

        var items = await _items.GetByIdsAsync(new List<Guid>(itemIds), cancellationToken);
        var byId = new Dictionary<Guid, Item>(items.Count);
        foreach (var item in items)
        {
            byId[item.Id] = item;
        }

        return byId;
    }
}
