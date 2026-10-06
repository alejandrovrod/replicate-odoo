namespace Erp.Domain.Common;

/// <summary>
/// Transversal paginated envelope returned by every flat list endpoint (Standard Pagination
/// Pattern): the page <see cref="Items"/> plus the metadata the universal paginator needs.
/// <c>TotalPages</c> is derived so producers can never disagree with the inputs.
/// </summary>
/// <typeparam name="T">Row type (entity at the repository boundary, DTO at the API boundary).</typeparam>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int PageNumber,
    int PageSize)
{
    /// <summary>How many pages the total spans at this page size (0 when empty).</summary>
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    /// <summary>
    /// Rebuilds the envelope around mapped items (entity -&gt; DTO in handlers), preserving the
    /// source metadata so handlers never recompute counts.
    /// </summary>
    public PagedResult<TDestination> Map<TDestination>(IReadOnlyList<TDestination> items)
        => new(items, TotalCount, PageNumber, PageSize);

    /// <summary>An empty page that still carries the requested paging (no-results case).</summary>
    public static PagedResult<T> Empty(PagedRequest paging)
        => new(Array.Empty<T>(), 0, paging.SafePageNumber, paging.SafePageSize);
}
