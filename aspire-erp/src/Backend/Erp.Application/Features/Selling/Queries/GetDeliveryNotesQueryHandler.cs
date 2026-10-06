using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Selling.Queries;

/// <summary>
/// Assembles <see cref="GetDeliveryNotesQuery"/>: notes from <see cref="IDeliveryNoteRepository"/>,
/// line item codes/names from <see cref="IItemRepository"/>. Tenant isolation is automatic
/// (Constitution II.3); CompanyId is business scoping.
/// </summary>
public sealed class GetDeliveryNotesQueryHandler
    : IQueryHandler<GetDeliveryNotesQuery, PagedResult<DeliveryNoteDto>>
{
    private readonly IDeliveryNoteRepository _deliveryNotes;
    private readonly IItemRepository _items;

    public GetDeliveryNotesQueryHandler(IDeliveryNoteRepository deliveryNotes, IItemRepository items)
    {
        _deliveryNotes = deliveryNotes;
        _items = items;
    }

    public async Task<PagedResult<DeliveryNoteDto>> HandleAsync(
        GetDeliveryNotesQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = await _deliveryNotes.GetRecentDeliveryNotesByCompanyAsync(
            query.CompanyId,
            new PagedRequest(query.PageNumber, query.PageSize),
            cancellationToken);
        if (page.Items.Count == 0)
        {
            return page.Map(new List<DeliveryNoteDto>());
        }

        var itemById = await LoadItemsAsync(page.Items, cancellationToken);

        var result = new List<DeliveryNoteDto>(page.Items.Count);
        foreach (var note in page.Items)
        {
            result.Add(DeliveryNoteDto.Build(note, itemById));
        }

        return page.Map(result);
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
