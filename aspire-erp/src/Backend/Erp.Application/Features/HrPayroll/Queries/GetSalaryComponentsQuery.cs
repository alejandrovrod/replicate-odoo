using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;

namespace Erp.Application.Features.HrPayroll.Queries;

/// <summary>Lists one company's salary components, ordered by name. Read-only.</summary>
public sealed record GetSalaryComponentsQuery(Guid CompanyId, int PageNumber = 1, int PageSize = 50) : IQuery<PagedResult<SalaryComponentDto>>;
