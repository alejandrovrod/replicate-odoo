using System.Collections.Generic;

namespace AssetHub.Application.Auth;

public class CurrentUserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<string> Roles { get; set; } = new();
    public List<string> Permissions { get; set; } = new();
    public string? TenantSlug { get; set; }
    public string? TenantName { get; set; }
    public string AvatarUrl { get; set; } = string.Empty;
    public string PreferredLocale { get; set; } = "es";
    public bool TwoFactorEnabled { get; set; }
    public EmployeeLinkDto? LinkedEmployee { get; set; }
}

public class EmployeeLinkDto
{
    public Guid EmployeeId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string RoleLabel { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
}