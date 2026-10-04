using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Manufacturing.Queries;

/// <summary>Loads one BOM with its lines and operations (Task 9.5 tree reads). Null when missing.</summary>
public sealed record GetBomDetailQuery(Guid CompanyId, Guid BomId) : IQuery<BomDto?>;
