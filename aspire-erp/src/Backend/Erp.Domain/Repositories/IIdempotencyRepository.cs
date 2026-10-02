using Erp.Domain.Entities;

namespace Erp.Domain.Repositories;

/// <summary>
/// Data-access contract for the idempotency ledger of <c>Idempotency-Key</c> values
/// (Constitution Article VI.4 / decision D7). Implemented by
/// Erp.Infrastructure.Data.Repositories.IdempotencyRepository.
/// </summary>
/// <remarks>
/// The reservation INSERT relies on the UNIQUE (TenantId, Key) index for atomicity: two
/// concurrent requests carrying the same key can never both win.
/// </remarks>
public interface IIdempotencyRepository
{
    /// <summary>The stored record for this key, or null when the key is new (this tenant only).</summary>
    Task<IdempotencyRecord?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically reserves the key as PENDING. Returns false when another request already
    /// reserved it (the caller then classifies the existing record through <see cref="GetByKeyAsync"/>).
    /// </summary>
    Task<bool> TryReserveAsync(string key, string requestHash, CancellationToken cancellationToken = default);

    /// <summary>Marks a completed request: stores the status and body to replay verbatim.</summary>
    Task CompleteAsync(string key, int responseStatus, string responseBody, CancellationToken cancellationToken = default);

    /// <summary>Releases a failed reservation (4xx/5xx/exception) so the client may retry the key.</summary>
    Task ReleaseAsync(string key, CancellationToken cancellationToken = default);
}
