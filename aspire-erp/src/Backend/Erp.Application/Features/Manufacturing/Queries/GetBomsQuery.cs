using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;

namespace Erp.Application.Features.Manufacturing.Queries;

/// <summary>Lists one company's BOMs with their lines and operations (Task 9.5 tree reads). Paginated (Standard Pagination Pattern): page 1 of 50 by default.</summary>
public sealed record GetBomsQuery(Guid CompanyId, int PageNumber = 1, int PageSize = 50) : IQuery<PagedResult<BomDto>>;
