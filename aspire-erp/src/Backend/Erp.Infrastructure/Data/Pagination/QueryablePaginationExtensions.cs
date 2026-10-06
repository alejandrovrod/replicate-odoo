using Erp.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Data.Pagination;

/// <summary>
/// Single implementation of the Standard Pagination Pattern: one COUNT plus one
/// SKIP/TAKE round-trip per list read. Repositories order first (deterministic pages require
/// ORDER BY) and delegate here — no endpoint reimplements the two queries.
/// </summary>
public static class QueryablePaginationExtensions
{
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        PagedRequest paging,
        CancellationToken cancellationToken = default)
    {
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip(paging.Skip)
            .Take(paging.SafePageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>(items, totalCount, paging.SafePageNumber, paging.SafePageSize);
    }
}
