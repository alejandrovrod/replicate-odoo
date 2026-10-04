using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Selling.Queries;

/// <summary>
/// Loads the most recent delivery notes of one company with their lines - the shipment history
/// behind Task 5.2b. Defaults to the 50 newest notes.
/// </summary>
public sealed record GetDeliveryNotesQuery(Guid CompanyId, int Limit = 50)
    : IQuery<IReadOnlyList<DeliveryNoteDto>>;
