using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Assets.Queries;

/// <summary>Lists one company's asset categories (Block B reads). Read-only.</summary>
public sealed record GetAssetCategoriesQuery(Guid CompanyId) : IQuery<IReadOnlyList<AssetCategoryDto>>;
