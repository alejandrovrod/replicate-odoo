using Erp.Api.Common;
using Erp.Api.Localization;
using Erp.Api.Shared;
using Erp.Application.Common;
using Erp.Application.Features.Tenants;
using Erp.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Localization;

namespace Erp.Api.Controllers.V1;

/// <summary>
/// Public tenant onboarding and host-resolution endpoints. These three routes are exempt from
/// <c>TenantResolutionMiddleware</c> because they must work before a tenant context exists.
/// </summary>
/// <remarks>
/// All failures are RFC 7807 <see cref="ProblemDetails"/> with the stable error code in
/// <c>extensions.code</c> (same contract as the other V1 controllers). Registration is atomic:
/// the handler either creates the tenant, company, COA, RBAC matrix and administrator together,
/// or persists nothing.
/// </remarks>
[ApiController]
[Route("api/v1/tenants")]
[AllowAnonymous]
[Produces("application/json")]
public sealed class TenantsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IMemoryCache _cache;
    private readonly IStringLocalizer<ErrorMessages> _errors;
    private readonly IStringLocalizer<CommonMessages> _common;
    private readonly ILogger<TenantsController> _logger;

    public TenantsController(
        ISender sender,
        IMemoryCache cache,
        IStringLocalizer<ErrorMessages> errors,
        IStringLocalizer<CommonMessages> common,
        ILogger<TenantsController> logger)
    {
        _sender = sender;
        _cache = cache;
        _errors = errors;
        _common = common;
        _logger = logger;
    }

    /// <summary>
    /// Live subdomain availability for the registration form. Invalid and reserved names are
    /// reported as unavailable (HTTP 200); the register endpoint returns the precise code.
    /// </summary>
    [HttpGet("check-availability")]
    [ProducesResponseType(typeof(SubdomainAvailabilityDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckAvailability(
        [FromQuery] string? subdomain,
        CancellationToken cancellationToken)
    {
        var normalized = TenantSubdomain.Normalize(subdomain);
        var available = await _sender.SendAsync(
            new CheckSubdomainAvailabilityQuery(normalized),
            cancellationToken);

        return Ok(new SubdomainAvailabilityDto(normalized, available));
    }

    /// <summary>Metadata for the active tenant that owns the subdomain (SPA boot resolution).</summary>
    [HttpGet("by-subdomain/{slug}")]
    [ProducesResponseType(typeof(TenantLookupDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBySubdomain(
        string slug,
        CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(new GetTenantBySubdomainQuery(slug), cancellationToken);
        return result.IsSuccess ? Ok(result.Value!) : MapFailure(result.Error!);
    }

    /// <summary>
    /// Self-service registration: tenant + first company + standard COA + RBAC + initial
    /// System Manager, atomically.
    /// </summary>
    [HttpPost("register")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(TenantRegistrationResult), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterTenantRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(
            new RegisterTenantCommand(
                request.TenantName ?? string.Empty,
                request.CompanyName ?? string.Empty,
                request.Subdomain ?? string.Empty,
                request.AdminFullName ?? string.Empty,
                request.AdminEmail ?? string.Empty,
                request.AdminPassword ?? string.Empty,
                request.CompanyTaxId,
                request.DefaultLanguage ?? TenantLanguage.Default),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return MapFailure(result.Error!);
        }

        var created = result.Value!;

        // A previous negative probe is not cached; a positive host entry from before the
        // registration (not possible for this subdomain) would be stale, so clear defensively.
        _cache.Remove(TenantSubdomainCache.Key(created.Subdomain));

        TenantAuditLog.TenantRegistered(
            _logger,
            created.TenantId,
            created.CompanyId,
            created.AdminUserId,
            created.Subdomain);

        return CreatedAtAction(
            nameof(GetBySubdomain),
            new { slug = created.Subdomain },
            created);
    }

    private ObjectResult MapFailure(Error error) => error.Code switch
    {
        TenantErrorCodes.TenantNotFound => Problem(
            StatusCodes.Status404NotFound,
            _common.Text("NotFound"),
            _errors.Text(error.Code, error.Message),
            error.Code),
        TenantErrorCodes.SubdomainTaken => Problem(
            StatusCodes.Status409Conflict,
            _common.Text("Conflict"),
            _errors.Text(error.Code, error.Message),
            error.Code),
        _ => Problem(
            StatusCodes.Status400BadRequest,
            _common.Text("BadRequest"),
            _errors.Text(error.Code, error.Message),
            error.Code),
    };

    private ObjectResult Problem(int status, string title, string detail, string? code)
    {
        var problem = new ProblemDetails
        {
            Type = status switch
            {
                StatusCodes.Status404NotFound => "https://tools.ietf.org/html/rfc9110#section-15.5.5",
                StatusCodes.Status409Conflict => "https://tools.ietf.org/html/rfc9110#section-15.5.10",
                _ => "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            },
            Title = title,
            Status = status,
            Detail = detail,
            Instance = HttpContext.Request.Path.Value,
        };

        if (code is not null)
        {
            problem.Extensions["code"] = code;
        }

        return new ObjectResult(problem) { StatusCode = status };
    }
}

/// <summary>Registration payload; every string is nullable so the handler owns validation.</summary>
public sealed record RegisterTenantRequest(
    string? TenantName,
    string? CompanyName,
    string? Subdomain,
    string? AdminFullName,
    string? AdminEmail,
    string? AdminPassword,
    string? CompanyTaxId,
    string? DefaultLanguage);
