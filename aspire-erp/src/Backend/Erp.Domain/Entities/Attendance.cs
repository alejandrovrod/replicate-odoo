using System;
using Erp.Domain.Common;

namespace Erp.Domain.Entities;

public enum LeaveType
{
    Sick = 1,
    Vacation = 2,
    Unpaid = 3
}

public class LeaveApplication : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public Guid EmployeeId { get; set; }
    
    public LeaveType LeaveType { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public decimal TotalLeaveDays { get; set; }

    public string Status { get; set; } = "Approved";
    public string? Reason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

public class Attendance : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public Guid EmployeeId { get; set; }

    public DateOnly AttendanceDate { get; set; }
    public string Status { get; set; } = "Present"; // Present, Absent, Half Day, On Leave
    public string? Shift { get; set; }

    public DateTimeOffset? InTime { get; set; }
    public DateTimeOffset? OutTime { get; set; }
}
