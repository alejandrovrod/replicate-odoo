using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;

namespace Erp.Application.Features.Assets.Queries;

/// <summary>Lists one company's asset categories (Block B reads). Read-only. Paginated (Standard Pagination Pattern): page 1 of 50 by default.</summary>
public sealed record GetAssetCategoriesQuery(Guid CompanyId, int PageNumber = 1, int PageSize = 50) : IQuery<PagedResult<AssetCategoryDto>>;
