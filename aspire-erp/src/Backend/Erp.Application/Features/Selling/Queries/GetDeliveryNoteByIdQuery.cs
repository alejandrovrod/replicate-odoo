using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Selling.Queries;

/// <summary>
/// Loads ONE delivery note with its lines, or null when it does not exist in this tenant/company
/// - the API turns null into RFC 7807 404.
/// </summary>
public sealed record GetDeliveryNoteByIdQuery(Guid CompanyId, Guid DeliveryNoteId)
    : IQuery<DeliveryNoteDto?>;
