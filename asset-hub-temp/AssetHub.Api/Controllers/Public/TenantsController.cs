using System.Threading.Tasks;
using AssetHub.Application.Tenancy.Commands;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AssetHub.Api.Controllers.Public;

[ApiController]
[Route("api/v1/public/tenants")]
public class PublicTenantsController : ControllerBase
{
    private readonly IMediator _mediator;

    public PublicTenantsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("check-slug")]
    public async Task<IActionResult> CheckSlug([FromBody] CheckSlugRequest request)
    {
        var result = await _mediator.Send(new CheckSlugCommand(request.Slug));
        return Ok(new { available = result.Available });
    }

    [HttpPost]
    public async Task<IActionResult> SignUp([FromBody] SignUpRequest request)
    {
        var command = new SignUpTenantCommand(
            request.OrgName,
            request.Slug,
            request.AdminName,
            request.Email,
            request.Password,
            request.PlanCode
        );

        var result = await _mediator.Send(command);
        return Accepted(new { tenantId = result.TenantId, status = result.Status });
    }
}

public class CheckSlugRequest
{
    public string Slug { get; set; } = string.Empty;
}

public class SignUpRequest
{
    public string OrgName { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string AdminName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string PlanCode { get; set; } = string.Empty;
}
