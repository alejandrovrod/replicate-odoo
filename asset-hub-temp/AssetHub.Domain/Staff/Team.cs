using System;
using System.Collections.Generic;

namespace AssetHub.Domain.Staff;

public class Team
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    
    public bool IsDeleted { get; set; }
    
    public ICollection<TeamMember> Members { get; set; } = new List<TeamMember>();
}
