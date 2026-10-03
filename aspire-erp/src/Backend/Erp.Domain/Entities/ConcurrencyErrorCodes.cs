namespace Erp.Domain.Entities;

/// <summary>
/// Stable machine-readable failure codes for concurrency control, shared by every module: one
/// concept (an optimistic-locking collision), one wire value (Constitution: shared vocabularies
/// keep the same value across modules, like the stock/buying document rules). Mirrors the
/// module-specific *ErrorCodes classes; specs: 02-stock ST-06, 04-buying BY-06, 05-banking,
/// 08-crm.
/// </summary>
public static class ConcurrencyErrorCodes
{
    /// <summary>The row was modified by another operation between this request's load and save.</summary>
    public const string ConcurrencyConflict = "concurrency_conflict";
}
