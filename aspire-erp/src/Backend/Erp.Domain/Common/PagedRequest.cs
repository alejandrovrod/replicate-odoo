namespace Erp.Domain.Common;

/// <summary>
/// Transversal pagination input for every flat list endpoint (Standard Pagination Pattern).
/// Lives in Domain (not Application.DTOs) because repository contracts — which live here —
/// take it as a parameter; Application re-exports the concept through its queries.
/// Normalization lives here so every layer shares one rule: pages start at 1, sizes clamp to
/// 1..500 (the 500 cap mirrors the historical per-endpoint <c>Limit</c> clamp).
/// Tree endpoints (account tree, warehouse tree) are deliberately excluded: expanding a
/// hierarchy requires full context, so they keep returning the whole tree.
/// </summary>
public sealed record PagedRequest(int PageNumber = 1, int PageSize = 50)
{
    /// <summary>Maximum page size accepted by any list endpoint.</summary>
    public const int MaxPageSize = 500;

    /// <summary>Default page size when the caller sends zero or a negative value.</summary>
    public const int DefaultPageSize = 50;

    /// <summary>1-based page number, floored at 1.</summary>
    public int SafePageNumber => PageNumber < 1 ? 1 : PageNumber;

    /// <summary>Page size clamped to 1..<see cref="MaxPageSize"/> (defaults to 50).</summary>
    public int SafePageSize => PageSize < 1 ? DefaultPageSize : Math.Min(PageSize, MaxPageSize);

    /// <summary>How many rows to skip for the safe page.</summary>
    public int Skip => (SafePageNumber - 1) * SafePageSize;
}
