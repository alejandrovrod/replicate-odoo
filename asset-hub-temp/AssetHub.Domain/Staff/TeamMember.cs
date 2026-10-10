using System;

namespace AssetHub.Domain.Staff;

public class TeamMember
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    
    public Guid TeamId { get; set; }
    public Team? Team { get; set; }
    
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    
    public bool IsLead { get; set; }
}
