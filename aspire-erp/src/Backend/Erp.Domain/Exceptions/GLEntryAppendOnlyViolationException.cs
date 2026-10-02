using Erp.Domain.Entities;

namespace Erp.Domain.Exceptions;

/// <summary>
/// Constitution Article III.2 (literal): GLEntry rows are INSERT-ONLY. Thrown by
/// <c>AppDbContext.SaveChanges</c> whenever a tracked GLEntry entry is Modified or Deleted -
/// the Constitution forbids silently clearing <c>IsModified</c>, so the only legal answer is to
/// throw and abort the save. The database trigger <c>trg_GLEntry_AppendOnly</c> enforces the
/// same law against raw SQL.
/// </summary>
public sealed class GLEntryAppendOnlyViolationException : Exception
{
    /// <summary>Stable failure code carried into the RFC 7807 <c>code</c> extension.</summary>
    public string Code { get; } = StockErrorCodes.GlEntryAppendOnly;

    public GLEntryAppendOnlyViolationException(string operation)
        : base(
            $"GLEntry is append-only: a {operation} operation on a General Ledger row "
            + "was rejected (Constitution III.2). Post a compensating reversal entry instead "
            + "(Constitution III.3).")
    {
    }
}
