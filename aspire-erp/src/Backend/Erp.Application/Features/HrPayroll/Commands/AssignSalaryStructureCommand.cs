using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.HrPayroll.Commands;

/// <summary>
/// Binds an employee to a salary structure from an effective start date (Task 12.2): both
/// sides must exist, be active and belong to the same company, and the new window must not
/// overlap any existing assignment of the employee. A rejected assignment writes zero rows.
/// </summary>
public sealed record AssignSalaryStructureCommand(
    Guid CompanyId,
    Guid EmployeeId,
    Guid StructureId,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo = null) : ICommand<Result<SalaryStructureAssignmentDto>>;
