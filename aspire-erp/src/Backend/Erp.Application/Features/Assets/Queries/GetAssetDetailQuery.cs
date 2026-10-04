using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Assets.Queries;

/// <summary>Returns one company's asset with its schedule lines (Block B reads). Read-only.</summary>
public sealed record GetAssetDetailQuery(Guid CompanyId, Guid AssetId) : IQuery<AssetDetailDto?>;
