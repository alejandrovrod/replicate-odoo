using System;

namespace AssetHub.Application.Maintenance.Dtos;

public class ScheduleOrderRequest
{
    public Guid? AssignedEmployeeId { get; set; }
    public DateTime? ScheduledStart { get; set; }
    public DateTime? ScheduledEnd { get; set; }
}
