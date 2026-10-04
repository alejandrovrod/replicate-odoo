using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Selling.Queries;

/// <summary>
/// Loads ONE customer by id, or null when it does not exist in this tenant/company - the API
/// turns null into RFC 7807 404 (mirrors <see cref="GetJournalEntryQuery"/>).
/// </summary>
public sealed record GetCustomerByIdQuery(Guid CompanyId, Guid CustomerId) : IQuery<CustomerDto?>;
