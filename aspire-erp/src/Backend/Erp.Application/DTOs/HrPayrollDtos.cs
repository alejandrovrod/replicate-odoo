using System;
using Erp.Domain.Entities;

namespace Erp.Application.DTOs;

public record AttendanceDto(
    Guid Id,
    Guid CompanyId,
    Guid EmployeeId,
    DateOnly AttendanceDate,
    string Status,
    string? Shift,
    DateTimeOffset? InTime,
    DateTimeOffset? OutTime
);

public record LeaveApplicationDto(
    Guid Id,
    Guid CompanyId,
    Guid EmployeeId,
    string LeaveType,
    DateOnly FromDate,
    DateOnly ToDate,
    decimal TotalLeaveDays,
    string Status,
    string? Reason,
    DateTimeOffset CreatedAt
);
