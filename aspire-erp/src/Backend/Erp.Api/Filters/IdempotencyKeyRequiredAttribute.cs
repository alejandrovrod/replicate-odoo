using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Filters;

/// <summary>
/// Constitution Article VI.4 (literal attribute name): marks a mutation endpoint that executes
/// ledger postings and therefore REQUIRES the <c>Idempotency-Key</c> header. Implemented as a
/// <see cref="ServiceFilterAttribute"/> so the filter itself is resolved from DI (a plain
/// attribute cannot take constructor dependencies).
/// </summary>
/// <remarks>
/// Applied to <c>POST /api/v1/stockentries</c> only: VI.4 is scoped to ledger-posting mutations,
/// and item/warehouse creation writes no GLEntry rows, so those endpoints stay filter-free.
/// </remarks>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class IdempotencyKeyRequiredAttribute : ServiceFilterAttribute
{
    public IdempotencyKeyRequiredAttribute()
        : base(typeof(IdempotencyFilter))
    {
    }
}
