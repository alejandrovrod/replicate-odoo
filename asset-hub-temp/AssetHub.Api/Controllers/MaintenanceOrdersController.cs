using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AssetHub.Application.Maintenance.Commands;
using AssetHub.Application.Maintenance.Dtos;
using AssetHub.Application.Maintenance.Queries;
using AssetHub.Infrastructure.Billing;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetHub.Api.Controllers;

[ApiController]
[Route("api/v1/maintenance-orders")]
[Authorize]
[RequirePlanLimits("maintenance")]
public class MaintenanceOrdersController : ControllerBase
{
    private readonly IMediator _mediator;

    public MaintenanceOrdersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<GetMaintenanceOrdersResult>> GetOrders(
        [FromQuery] string? state,
        [FromQuery] string? kind,
        [FromQuery] Guid? assetId,
        [FromQuery] Guid? preventivePlanId,
        [FromQuery] Guid? incidentId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetMaintenanceOrdersQuery
        {
            State = state,
            Kind = kind,
            AssetId = assetId,
            PreventivePlanId = preventivePlanId,
            IncidentId = incidentId,
            Search = search,
            Page = page,
            PageSize = pageSize
        });

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<MaintenanceOrderDetailDto>> GetOrderById(Guid id)
    {
        var result = await _mediator.Send(new GetMaintenanceOrderByIdQuery(id));
        if (result == null)
            return NotFound();

        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateMaintenanceOrderCommand command)
    {
        var id = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetOrderById), new { id }, new { id });
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<MaintenanceOrderSummaryDto>> UpdateOrder(Guid id, [FromBody] UpdateMaintenanceOrderCommand command)
    {
        command.MaintenanceOrderId = id;
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteOrder(Guid id)
    {
        await _mediator.Send(new DeleteMaintenanceOrderCommand { MaintenanceOrderId = id });
        return NoContent();
    }

    [HttpPatch("{id}/approve")]
    public async Task<IActionResult> ApproveOrder(Guid id)
    {
        var command = new ApproveMaintenanceOrderCommand { MaintenanceOrderId = id };
        await _mediator.Send(command);
        return NoContent();
    }

    [HttpPatch("{id}/schedule")]
    public async Task<IActionResult> ScheduleOrder(Guid id, [FromBody] ScheduleOrderRequest request)
    {
        var command = new ScheduleMaintenanceOrderCommand
        {
            MaintenanceOrderId = id,
            AssignedEmployeeId = request.AssignedEmployeeId,
            ScheduledStart = request.ScheduledStart,
            ScheduledEnd = request.ScheduledEnd
        };
        await _mediator.Send(command);
        return NoContent();
    }

    [HttpPut("{id}/costs")]
    public async Task<IActionResult> RecordCosts(Guid id, [FromBody] RecordMaintenanceCostsCommand command)
    {
        command.MaintenanceOrderId = id;
        await _mediator.Send(command);
        return NoContent();
    }

    [HttpPatch("{id}/verify")]
    public async Task<IActionResult> VerifyOrder(Guid id)
    {
        var command = new VerifyMaintenanceOrderCommand { MaintenanceOrderId = id };
        await _mediator.Send(command);
        return NoContent();
    }

    [HttpPatch("{id}/reject")]
    public async Task<IActionResult> RejectOrder(Guid id, [FromBody] List<Guid> approvedTaskIds)
    {
        var command = new RejectMaintenanceOrderCommand { MaintenanceOrderId = id, ApprovedTaskIds = approvedTaskIds ?? new List<Guid>() };
        await _mediator.Send(command);
        return NoContent();
    }

    [HttpPatch("{id}/start")]
    public async Task<IActionResult> StartOrder(Guid id)
    {
        var command = new StartMaintenanceOrderCommand { MaintenanceOrderId = id };
        await _mediator.Send(command);
        return NoContent();
    }

    [HttpPatch("{id}/complete")]
    public async Task<IActionResult> CompleteOrder(Guid id)
    {
        var command = new CompleteMaintenanceOrderCommand { MaintenanceOrderId = id };
        await _mediator.Send(command);
        return NoContent();
    }

    [HttpPatch("{id}/cancel")]
    public async Task<IActionResult> CancelOrder(Guid id)
    {
        var command = new CancelMaintenanceOrderCommand { MaintenanceOrderId = id };
        await _mediator.Send(command);
        return NoContent();
    }

    [HttpGet("{id}/tasks")]
    public async Task<ActionResult<MaintenanceOrderTaskSummaryDto[]>> GetOrderTasks(Guid id)
    {
        var tasks = await _mediator.Send(new GetMaintenanceOrderTasksQuery(id));
        return Ok(tasks);
    }

    [HttpGet("{id}/parts")]
    public async Task<ActionResult<MaintenanceOrderPartDto[]>> GetOrderParts(Guid id)
    {
        var parts = await _mediator.Send(new GetMaintenanceOrderPartsQuery(id));
        return Ok(parts);
    }

    [HttpPost("{id}/parts")]
    public async Task<IActionResult> AddPart(Guid id, [FromBody] AddPartRequest request)
    {
        var command = new AddMaintenancePartCommand
        {
            MaintenanceOrderId = id,
            CatalogItemId = request.CatalogItemId,
            Quantity = request.Quantity,
            UnitCost = request.UnitCost,
            SourceType = request.SourceType,
            WarehouseId = request.WarehouseId,
            ExternalSupplierName = request.ExternalSupplierName,
            ExternalReference = request.ExternalReference
        };
        var partId = await _mediator.Send(command);
        return Ok(new { id = partId });
    }

    [HttpDelete("{id}/parts/{partId}")]
    public async Task<IActionResult> RemovePart(Guid id, Guid partId)
    {
        await _mediator.Send(new RemoveMaintenancePartCommand { MaintenanceOrderId = id, PartId = partId });
        return NoContent();
    }
}
