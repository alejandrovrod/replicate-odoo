using System;

namespace AssetHub.Domain.Staff;

public class EmployeeAvailability
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    
    public int DayOfWeek { get; set; } // 0 = Sunday, 1 = Monday, etc.
    
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    
    public bool IsAvailable { get; set; } = true;
}
