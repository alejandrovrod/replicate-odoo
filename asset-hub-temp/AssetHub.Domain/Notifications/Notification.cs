using System;

namespace AssetHub.Domain.Notifications;

public class Notification
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public Guid UserId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? RelatedEntityType { get; set; }
    public Guid? RelatedEntityId { get; set; }
}
