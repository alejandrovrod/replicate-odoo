using System;
using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.HrPayroll.Commands;

public sealed record CreateAttendanceCommand(
    Guid CompanyId,
    Guid EmployeeId,
    DateOnly AttendanceDate,
    string Status = "Present",
    string? Shift = null,
    DateTimeOffset? InTime = null,
    DateTimeOffset? OutTime = null
) : ICommand<Result<AttendanceDto>>;

public sealed record CreateLeaveApplicationCommand(
    Guid CompanyId,
    Guid EmployeeId,
    string LeaveType,
    DateOnly FromDate,
    DateOnly ToDate,
    decimal TotalLeaveDays,
    string? Reason = null
) : ICommand<Result<LeaveApplicationDto>>;
