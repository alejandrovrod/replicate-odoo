using System.Threading.Tasks;
using AssetHub.Application.Billing.Commands;
using AssetHub.Application.Billing.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetHub.Api.Controllers;

[ApiController]
[Route("api/v1/billing")]
[Authorize]
public class BillingController : ControllerBase
{
    private readonly IMediator _mediator;

    public BillingController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("subscription")]
    public async Task<IActionResult> GetSubscription()
    {
        var result = await _mediator.Send(new GetSubscriptionStatusQuery());
        return Ok(result);
    }

    [HttpPost("checkout")]
    public IActionResult Checkout([FromBody] CheckoutRequest request)
    {
        // Redirige al portal (simulado en modo manual, Stripe checkout en real)
        return Accepted(new { checkoutUrl = "https://assethub.app/contact-sales" });
    }

    [HttpPost("change-plan")]
    public async Task<IActionResult> ChangePlan([FromBody] ChangePlanRequest request)
    {
        var success = await _mediator.Send(new ChangePlanCommand(request.PlanCode));
        return Ok(new { success });
    }

    [AllowAnonymous]
    [HttpPost("webhooks/stripe")]
    public async Task<IActionResult> StripeWebhook()
    {
        // Leer raw body
        var payload = "dummy_payload"; // En real se lee Request.Body
        var signature = Request.Headers["Stripe-Signature"].ToString();

        await _mediator.Send(new ProcessStripeWebhookCommand(payload, signature));
        return Ok();
    }
}

public class CheckoutRequest
{
    public string PlanCode { get; set; } = string.Empty;
    public string Interval { get; set; } = "monthly";
}

public class ChangePlanRequest
{
    public string PlanCode { get; set; } = string.Empty;
}
