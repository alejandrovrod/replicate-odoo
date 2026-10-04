using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Manufacturing.Queries;

/// <summary>Lists one company's BOMs with their lines and operations (Task 9.5 tree reads).</summary>
public sealed record GetBomsQuery(Guid CompanyId) : IQuery<IReadOnlyList<BomDto>>;
