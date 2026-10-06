using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;

namespace Erp.Application.Features.HrPayroll.Queries;

/// <summary>Lists one company's salary structures with their priced lines. Read-only.</summary>
public sealed record GetSalaryStructuresQuery(Guid CompanyId, int PageNumber = 1, int PageSize = 50) : IQuery<PagedResult<SalaryStructureDto>>;
