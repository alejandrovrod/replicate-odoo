using System;

namespace AssetHub.Domain.Security;

public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public Guid? ReplacedByTokenId { get; set; }
    public DateTime? RevokedAt { get; set; }
    public Guid FamilyId { get; set; }
    
    public bool IsActive => RevokedAt == null && !IsExpired;
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
}
