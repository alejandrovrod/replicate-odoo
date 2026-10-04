using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Manufacturing.Commands;

/// <summary>
/// Creates one work order in Draft with its gapless WO voucher (Task 9.3): no stock and no GL
/// impact - the number is assigned inside the creation transaction, mirroring the purchase-order
/// precedent. The referenced BOM only needs to EXIST here; the active + default acceptance gate
/// runs on <see cref="SubmitWorkOrderCommand"/>.
/// </summary>
public sealed record CreateWorkOrderCommand(
    Guid CompanyId,
    Guid ProductionItemId,
    Guid BomId,
    decimal QuantityToProduce,
    Guid SourceWarehouseId,
    Guid WipWarehouseId,
    Guid TargetWarehouseId,
    DateOnly PlannedStartDate,
    DateOnly PlannedEndDate) : ICommand<Result<WorkOrderDto>>;
