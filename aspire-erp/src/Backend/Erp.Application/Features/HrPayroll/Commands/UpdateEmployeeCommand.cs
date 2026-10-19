using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;

namespace Erp.Application.Features.HrPayroll.Commands;

/// <summary>
/// Updates one employee's staff record: identity, employment dates, pay mode/status and bank
/// details. Carries the caller's <see cref="RowVersion"/> (base64-decoded rowversion) for
/// optimistic concurrency - a stale token fails with <c>concurrency_conflict</c>, never a
/// silent overwrite. The salary structure assignment is NOT part of this contract (windows are
/// managed by the assignment flow, created with the employee).
/// </summary>
public sealed record UpdateEmployeeCommand(
    Guid CompanyId,
    Guid EmployeeId,
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
    byte[]? RowVersion = null) : ICommand<Result<EmployeeDto>>;
