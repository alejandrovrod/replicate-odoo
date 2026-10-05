using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.HrPayroll.Queries;

/// <summary>Lists one company's structure assignments (the eligibility windows). Read-only.</summary>
public sealed record GetStructureAssignmentsQuery(Guid CompanyId) : IQuery<IReadOnlyList<SalaryStructureAssignmentDto>>;
