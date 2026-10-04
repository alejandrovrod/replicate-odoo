using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Assets.Queries;

/// <summary>Lists one company's asset headers (Block B reads). Read-only.</summary>
public sealed record GetAssetsQuery(Guid CompanyId) : IQuery<IReadOnlyList<AssetDto>>;
