using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Manufacturing.Commands;

/// <summary>
/// Completes an InProcess work order (Task 9.4, spec MF-03): consumes the BOM components from the
/// WIP warehouse at FIFO cost, capitalizes operating cost from the BOM's planned operations, and
/// receives the finished goods into the target warehouse at the cost-engine unit rate
/// (Dr 1330 TotalCost / Cr 1320 RawMaterialCost / Cr 5210 OperatingCost). On success the order
/// advances InProcess -&gt; Completed with ProducedQuantity and ActualEndDate stamped.
/// </summary>
public sealed record CompleteManufactureCommand(
    Guid CompanyId,
    Guid WorkOrderId,
    decimal ProducedQuantity,
    DateOnly? PostingDate = null) : ICommand<Result<StockEntryPostingDto>>;
