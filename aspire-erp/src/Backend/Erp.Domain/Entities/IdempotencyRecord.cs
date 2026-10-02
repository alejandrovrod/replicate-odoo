using Erp.Domain.Common;

namespace Erp.Domain.Entities;

/// <summary>
/// One row per client-supplied <c>Idempotency-Key</c> (Constitution Article VI.4). The unique
/// (TenantId, Key) index makes the reservation itself atomic: two concurrent requests with the
/// same key can never both run the ledger posting.
/// </summary>
/// <remarks>
/// Lifecycle: inserted as a PENDING reservation (ResponseStatus null) before the action runs,
/// completed with the status/body on a 2xx outcome, and DELETED on 4xx/5xx/exception so the
/// client can legitimately retry with the same key.
/// </remarks>
public class IdempotencyRecord : ITenantEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant; immutable after creation (Constitution Article II.4).</summary>
    public Guid TenantId { get; set; }

    /// <summary>The client-supplied Idempotency-Key value (max 100 chars), unique per tenant.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>SHA-256 (hex) of the raw request body, used to detect key reuse with a new payload.</summary>
    public string RequestHash { get; set; } = string.Empty;

    /// <summary>HTTP status stored for a successful (2xx) execution; null while the request is in flight.</summary>
    public int? ResponseStatus { get; set; }

    /// <summary>Exact response body (JSON) to replay verbatim for a completed key.</summary>
    public string? ResponseBody { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
