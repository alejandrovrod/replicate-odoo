using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;

namespace Erp.Application.Features.HrPayroll.Commands;

/// <summary>
/// Registers one employee (spec ubiquitous language "Employee"): legal identity, employment
/// dates, pay mode and status, plus the OPTIONAL initial salary structure assignment created
/// atomically from the joining date. A rejected creation writes zero rows (employee and
/// assignment commit in ONE transaction).
/// </summary>
/// <remarks>
/// Department/Designation stay out of the write contract on purpose: they are org links, not
/// part of the staff-registration flow, and the aggregate already carries nullable FKs. The
/// domain shape rules run through the pure <see cref="EmployeeValidator"/>; employee-number
/// uniqueness needs a repository read and lives in the handler.
/// </remarks>
public sealed record CreateEmployeeCommand(
    Guid CompanyId,
    string EmployeeNumber,
    string FirstName,
    string LastName,
    string WorkEmail,
    DateOnly DateOfJoining,
    DateOnly? DateOfRelieving,
    SalaryMode SalaryMode,
    string? BankName,
    string? BankAccountNumber,
    EmploymentStatus Status,
    Guid? SalaryStructureId = null) : ICommand<Result<EmployeeDto>>;
