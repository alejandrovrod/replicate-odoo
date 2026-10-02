namespace Erp.Application.Common;

/// <summary>What the idempotency guard must do for one incoming request.</summary>
public enum IdempotencyDecision
{
    /// <summary>No record for this key: reserve it, run the action, then store or release it.</summary>
    Proceed,

    /// <summary>Same key AND same payload, already completed: replay the stored status + body verbatim.</summary>
    ReplayStoredResponse,

    /// <summary>Same key AND same payload, still executing: 409 - ask the client to retry later.</summary>
    RequestInProgress,

    /// <summary>Same key but a DIFFERENT payload: 409 Conflict - the key must not be reused.</summary>
    KeyReuseWithDifferentPayload,
}

/// <summary>
/// Pure decision logic behind the <c>[IdempotencyKeyRequired]</c> filter (Constitution VI.4 /
/// decision D7), kept in Erp.Application so it can be unit-tested without HTTP or a database.
/// </summary>
public static class IdempotencyPolicy
{
    /// <summary>True when the raw header value is present and non-empty after trimming.</summary>
    public static bool HasUsableKey(string? headerValue) => !string.IsNullOrWhiteSpace(headerValue);

    /// <summary>
    /// Classifies an incoming request against the stored record for the same key:
    /// <list type="bullet">
    /// <item><paramref name="storedRecordExists"/> = false -> <see cref="IdempotencyDecision.Proceed"/>.</item>
    /// <item>hash differs -> <see cref="IdempotencyDecision.KeyReuseWithDifferentPayload"/>.</item>
    /// <item>hash matches but no response stored yet -> <see cref="IdempotencyDecision.RequestInProgress"/>.</item>
    /// <item>hash matches and a response is stored -> <see cref="IdempotencyDecision.ReplayStoredResponse"/>.</item>
    /// </list>
    /// </summary>
    public static IdempotencyDecision Decide(
        bool storedRecordExists,
        string storedRequestHash,
        int? storedResponseStatus,
        string requestHash)
    {
        if (!storedRecordExists)
        {
            return IdempotencyDecision.Proceed;
        }

        if (!string.Equals(storedRequestHash, requestHash, StringComparison.Ordinal))
        {
            return IdempotencyDecision.KeyReuseWithDifferentPayload;
        }

        return storedResponseStatus is null
            ? IdempotencyDecision.RequestInProgress
            : IdempotencyDecision.ReplayStoredResponse;
    }
}
