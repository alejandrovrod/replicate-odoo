using Erp.Domain.Entities;

namespace Erp.Domain.Exceptions;

/// <summary>
/// Optimistic concurrency conflict (specs 02-stock ST-06, 04-buying BY-06): the row was changed
/// by another operation after it was loaded for this change, so the <c>RowVersion</c> check
/// rejected the save. Repositories translate EF's <c>DbUpdateConcurrencyException</c> into this
/// typed domain failure; CQRS handlers convert it into a <c>Result.Failure</c> whose
/// <see cref="Code"/> the API maps to HTTP 409.
/// </summary>
public sealed class ConcurrencyConflictException : Exception
{
    /// <summary>Stable failure code carried into the RFC 7807 <c>code</c> extension.</summary>
    public string Code { get; } = ConcurrencyErrorCodes.ConcurrencyConflict;

    public string EntityName { get; }

    public Guid EntityId { get; }

    public ConcurrencyConflictException(string entityName, Guid entityId, Exception? innerException = null)
        : base(
            $"The {entityName} '{entityId}' was modified by another operation while this change "
            + "was being saved (optimistic concurrency conflict). Reload it and retry.",
            innerException)
    {
        EntityName = entityName;
        EntityId = entityId;
    }
}
