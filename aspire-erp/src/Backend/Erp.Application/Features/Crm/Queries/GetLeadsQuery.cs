using Erp.Application.Common;
using Erp.Application.Features.Crm.DTOs;

namespace Erp.Application.Features.Crm.Queries;

/// <summary>Loads the most recent leads of a company - the Block B list read. Defaults to 50.</summary>
public sealed record GetLeadsQuery(Guid CompanyId, int Limit = 50)
    : IQuery<IReadOnlyList<LeadDto>>;
