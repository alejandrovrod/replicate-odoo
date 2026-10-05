using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.HrPayroll.Queries;

/// <summary>Lists one company's salary components, ordered by name. Read-only.</summary>
public sealed record GetSalaryComponentsQuery(Guid CompanyId) : IQuery<IReadOnlyList<SalaryComponentDto>>;
