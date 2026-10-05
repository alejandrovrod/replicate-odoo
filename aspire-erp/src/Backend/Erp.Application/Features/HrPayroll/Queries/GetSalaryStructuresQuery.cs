using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.HrPayroll.Queries;

/// <summary>Lists one company's salary structures with their priced lines. Read-only.</summary>
public sealed record GetSalaryStructuresQuery(Guid CompanyId) : IQuery<IReadOnlyList<SalaryStructureDto>>;
