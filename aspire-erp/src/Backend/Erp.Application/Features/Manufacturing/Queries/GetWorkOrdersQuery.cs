using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Manufacturing.Queries;

/// <summary>Lists one company's work-order headers (Task 9.5 execution board reads).</summary>
public sealed record GetWorkOrdersQuery(Guid CompanyId) : IQuery<IReadOnlyList<WorkOrderDto>>;
