namespace Erp.Domain.Common;

/// <summary>
/// Contract for entities that belong to exactly one tenant (Constitution Article II.1).
/// </summary>
/// <remarks>
/// Lives in Erp.Domain so tenant-scoped entities can implement it while the Domain project keeps
/// zero ProjectReferences (Constitution Article I.2). Application-level services
/// (<c>ITenantProvider</c>, <c>TenantProvider</c>) remain in Erp.Application.Common, which may
/// reference Domain.
/// </remarks>
public interface ITenantEntity
{
    Guid TenantId { get; set; }
}
