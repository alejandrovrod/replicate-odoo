using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Manufacturing.Commands;

/// <summary>
/// Executes <see cref="SubmitWorkOrderCommand"/>: Draft -&gt; Submitted (Task 9.3 workflow) after
/// the acceptance gate (BOM exists + <c>IsActive</c> + <c>IsDefault</c>). Any other current state
/// fails with <c>invalid_status_transition</c>, which the API maps to a 409. No stock and no GL
/// impact. A submit-time availability pre-check is deliberately deferred: the transfer (9.4)
/// and completion (9.4) postings enforce <c>InsufficientStockException</c> via FIFO consumption,
/// which is the hard guard that matters (spec MF-06).
/// </summary>
public sealed class SubmitWorkOrderCommandHandler
    : ICommandHandler<SubmitWorkOrderCommand, Result<WorkOrderDto>>
{
    private readonly IManufacturingRepository _manufacturing;

    public SubmitWorkOrderCommandHandler(IManufacturingRepository manufacturing)
    {
        _manufacturing = manufacturing;
    }

    public async Task<Result<WorkOrderDto>> HandleAsync(
        SubmitWorkOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var order = await _manufacturing.GetWorkOrderByIdAsync(command.WorkOrderId, cancellationToken)
                ?? throw new ManufacturingValidationException(
                    ManufacturingErrorCodes.WorkOrderNotFound,
                    $"Work order '{command.WorkOrderId}' was not found in this tenant.");

            if (order.CompanyId != command.CompanyId)
            {
                throw new ManufacturingValidationException(
                    ManufacturingErrorCodes.WorkOrderNotFound,
                    $"Work order '{order.OrderNumber}' does not belong to company '{command.CompanyId}'.");
            }

            var bom = await _manufacturing.GetBomByIdAsync(order.BomId, cancellationToken)
                ?? throw new ManufacturingValidationException(
                    ManufacturingErrorCodes.BomNotFound,
                    $"BOM '{order.BomId}' referenced by work order '{order.OrderNumber}' was not found in this tenant.");

            if (!bom.IsActive)
            {
                throw new ManufacturingValidationException(
                    ManufacturingErrorCodes.InactiveBom,
                    $"BOM '{bom.BomNumber}' is inactive and cannot authorize production of work order '{order.OrderNumber}'.");
            }

            if (!bom.IsDefault)
            {
                throw new ManufacturingValidationException(
                    ManufacturingErrorCodes.NonDefaultBom,
                    $"BOM '{bom.BomNumber}' is not the default recipe and cannot authorize production of work order '{order.OrderNumber}'.");
            }

            order.Submit();
            await _manufacturing.UpdateWorkOrderAsync(order, cancellationToken);

            return Result<WorkOrderDto>.Success(WorkOrderDto.Build(order));
        }
        catch (ManufacturingValidationException ex)
        {
            return Result<WorkOrderDto>.Failure(ex.Code, ex.Message);
        }
        catch (ConcurrencyConflictException ex)
        {
            // Spec MF-06: the order changed under our feet (RowVersion mismatch on save).
            return Result<WorkOrderDto>.Failure(ex.Code, ex.Message);
        }
    }
}
