using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;

namespace Erp.Application.Features.Selling.Queries;

/// <summary>
/// Loads the most recent delivery notes of one company with their lines - the shipment history
/// behind Task 5.2b. Paginated (Standard Pagination Pattern): page 1 of 50 by default.
/// </summary>
public sealed record GetDeliveryNotesQuery(Guid CompanyId, int PageNumber = 1, int PageSize = 50)
    : IQuery<PagedResult<DeliveryNoteDto>>;
