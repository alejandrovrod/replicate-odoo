using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AssetHub.Application.Inventory.Commands;
using AssetHub.Application.Inventory.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetHub.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize] // Requires auth, tenant isolation handled downstream
public class InventoryController : ControllerBase
{
    private readonly IMediator _mediator;

    public InventoryController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // SETTINGS

    [HttpGet("settings")]
    public async Task<ActionResult<TenantInventorySettingsDto>> GetSettings()
    {
        var result = await _mediator.Send(new GetInventorySettingsQuery());
        return Ok(result);
    }

    [HttpPut("settings")]
    public async Task<ActionResult<InventorySettingsDto>> UpdateSettings([FromBody] UpdateInventorySettingsCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    // WAREHOUSES

    [HttpPost("warehouses")]
    public async Task<ActionResult<Guid>> CreateWarehouse([FromBody] CreateWarehouseCommand command)
    {
        var id = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetWarehouses), new { id }, id);
    }

    [HttpGet("warehouses")]
    public async Task<ActionResult<List<WarehouseDto>>> GetWarehouses()
    {
        var result = await _mediator.Send(new GetWarehousesQuery());
        return Ok(result);
    }

    // STOCK AND TRANSACTIONS

    [HttpGet("stock")]
    public async Task<ActionResult<List<StockBalanceDto>>> GetStockBalances(
        [FromQuery] Guid? warehouseId, 
        [FromQuery] Guid? catalogItemId)
    {
        var result = await _mediator.Send(new GetStockBalancesQuery 
        { 
            WarehouseId = warehouseId,
            CatalogItemId = catalogItemId
        });
        return Ok(result);
    }

    [HttpPost("adjustments")]
    public async Task<ActionResult<Guid>> PostAdjustment([FromBody] PostInventoryAdjustmentCommand command)
    {
        // Enforce idempotency client-side, but if missing generate one
        if (string.IsNullOrWhiteSpace(command.IdempotencyKey))
        {
            command.IdempotencyKey = Guid.NewGuid().ToString();
        }

        var id = await _mediator.Send(command);
        return Ok(id);
    }
}
