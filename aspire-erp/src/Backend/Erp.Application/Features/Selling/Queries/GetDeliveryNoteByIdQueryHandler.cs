using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Selling.Queries;

/// <summary>Single-note read of <see cref="GetDeliveryNoteByIdQuery"/> (null = 404 at the API).</summary>
public sealed class GetDeliveryNoteByIdQueryHandler
    : IQueryHandler<GetDeliveryNoteByIdQuery, DeliveryNoteDto?>
{
    private readonly IDeliveryNoteRepository _deliveryNotes;
    private readonly IItemRepository _items;

    public GetDeliveryNoteByIdQueryHandler(IDeliveryNoteRepository deliveryNotes, IItemRepository items)
    {
        _deliveryNotes = deliveryNotes;
        _items = items;
    }

    public async Task<DeliveryNoteDto?> HandleAsync(
        GetDeliveryNoteByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var note = await _deliveryNotes.GetDeliveryNoteByIdAsync(query.DeliveryNoteId, cancellationToken);
        if (note is null || note.CompanyId != query.CompanyId)
        {
            return null;
        }

        var itemIds = new HashSet<Guid>();
        foreach (var line in note.Lines)
        {
            itemIds.Add(line.ItemId);
        }

        var items = await _items.GetByIdsAsync(new List<Guid>(itemIds), cancellationToken);
        var itemById = new Dictionary<Guid, Item>(items.Count);
        foreach (var item in items)
        {
            itemById[item.Id] = item;
        }

        return DeliveryNoteDto.Build(note, itemById);
    }
}
