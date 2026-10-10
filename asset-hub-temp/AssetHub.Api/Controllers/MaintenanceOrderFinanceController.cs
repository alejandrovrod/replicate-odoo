using System.Threading.Tasks;
using AssetHub.Application.Finance.Commands;
using AssetHub.Application.Finance.Dtos;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetHub.Api.Controllers;

[ApiController]
[Route("api/v1/maintenance-orders")]
[Authorize]
public class MaintenanceOrderFinanceController : ControllerBase
{
    private readonly IMediator _mediator;

    public MaintenanceOrderFinanceController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("{id}/capitalize")]
    [Authorize(Policy = "permission:assets.capitalization:propose")]
    public async Task<IActionResult> CapitalizeMaintenanceOrder(Guid id, [FromBody] CapitalizeMaintenanceRequest request)
    {
        var result = await _mediator.Send(new CapitalizeMaintenanceOrderCommand(id, request));
        return Created($"/api/v1/maintenance-orders/{id}/capitalize", result);
    }
}