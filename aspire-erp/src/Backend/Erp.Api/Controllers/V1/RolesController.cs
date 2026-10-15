using Erp.Api.Common;
using Erp.Api.Localization;
using Erp.Api.Shared;
using Erp.Application.Common;
using Erp.Application.Features.Security.Roles;
using Erp.Domain.Entities.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Erp.Api.Controllers.V1;

/// <summary>
/// Role &amp; Permission Manager endpoints (.specify/modules/role-permission-manager/spec.md §4).
/// Every mutation dispatches through the hand-rolled <see cref="ISender"/> (decision C2).
/// Authorization is server-side: <c>TenantMember</c> (tenant resolved) AND the
/// <c>System Manager</c> role (stacked attributes = AND, the CurrenciesController
/// stacking precedent). Role/permission changes apply at the affected user's next login
/// (JWT claims are minted at login; no server-side revocation exists).
/// </summary>
/// <remarks>
/// Domain error-code to status mapping (spec §4):
/// <list type="bullet">
/// <item><c>ROLE_NOT_FOUND / USER_ROLE_NOT_FOUND</c> -&gt; 404.</item>
/// <item><c>SYSTEM_MANAGER_IMMUTABLE / CROSS_TENANT_ASSIGNMENT_DENIED</c> -&gt; 403.</item>
/// <item><c>ROLE_NAME_CONFLICT / CANNOT_REMOVE_LAST_ADMIN / USER_ROLE_ALREADY_ASSIGNED / ROLE_HAS_ASSIGNMENTS</c> -&gt; 409.</item>
/// <item>Any other validation failure -&gt; 400.</item>
/// </list>
/// </remarks>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "TenantMember")]
[Authorize(Roles = "System Manager")]
[Produces("application/json")]
[Consumes("application/json")]
public sealed class RolesController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ITenantProvider _tenantProvider;
    private readonly IStringLocalizer<ErrorMessages> _errors;
    private readonly IStringLocalizer<CommonMessages> _common;

    public RolesController(
        ISender sender,
        ITenantProvider tenantProvider,
        IStringLocalizer<ErrorMessages> errors,
        IStringLocalizer<CommonMessages> common)
    {
        _sender = sender;
        _tenantProvider = tenantProvider;
        _errors = errors;
        _common = common;
    }

    /// <summary>Lists every role of the current tenant with headcounts (SEC-G2).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RoleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRoles(CancellationToken cancellationToken)
    {
        var roles = await _sender.SendAsync(new GetRolesQuery(), cancellationToken);
        return Ok(roles);
    }

    /// <summary>Creates a functional role (SEC-G2). Duplicate names in-tenant are a 409.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateRole(
        [FromBody] CreateRoleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(
            new CreateRoleCommand(request.Name, request.Description, _tenantProvider.GetCurrentTenantId()),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return MapFailure(result.Error!);
        }

        var dto = result.Value!;
        return CreatedAtAction(nameof(GetPermissions), new { id = dto.Id }, dto);
    }

    /// <summary>Official DocType catalog (spec §5) for the matrix editor.</summary>
    [HttpGet("doctypes-catalog")]
    [ProducesResponseType(typeof(IReadOnlyList<DocTypeCatalogEntry>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDocTypesCatalog(CancellationToken cancellationToken)
    {
        var catalog = await _sender.SendAsync(new GetDocTypesCatalogQuery(), cancellationToken);
        return Ok(catalog);
    }

    /// <summary>Every user of the current tenant with its role names (assignment dialog).</summary>
    [HttpGet("users-with-roles")]
    [ProducesResponseType(typeof(IReadOnlyList<UserWithRolesDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsersWithRoles(CancellationToken cancellationToken)
    {
        var users = await _sender.SendAsync(new GetUsersWithRolesQuery(), cancellationToken);
        return Ok(users);
    }

    /// <summary>Role detail with its permission matrix (SEC-G1).</summary>
    [HttpGet("{id:guid}/permissions")]
    [ProducesResponseType(typeof(RolePermissionsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPermissions(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(new GetRolePermissionsQuery(id), cancellationToken);

        if (!result.IsSuccess)
        {
            return MapFailure(result.Error!);
        }

        return Ok(result.Value!);
    }

    /// <summary>
    /// Replaces the permission matrix of a functional role (SEC-G1). System Manager has no
    /// rows: any update over it is <c>403 SYSTEM_MANAGER_IMMUTABLE</c>.
    /// </summary>
    [HttpPut("{id:guid}/permissions")]
    [ProducesResponseType(typeof(RolePermissionsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePermissions(
        Guid id,
        [FromBody] UpdateRolePermissionsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(
            new UpdateRolePermissionsCommand(
                id,
                _tenantProvider.GetCurrentTenantId(),
                request.Permissions ?? new List<DocTypePermissionDto>()),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return MapFailure(result.Error!);
        }

        return Ok(result.Value!);
    }

    /// <summary>
    /// Replaces the whole role set of a user - PUT semantics, idempotent (SEC-G3/SEC-G5).
    /// Removing System Manager from the tenant's last holder is
    /// <c>409 CANNOT_REMOVE_LAST_ADMIN</c>. Applies at the user's next login.
    /// </summary>
    [HttpPut("/api/v1/users/{userId:guid}/roles")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AssignUserRoles(
        Guid userId,
        [FromBody] AssignUserRolesRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(
            new AssignUserRolesCommand(
                userId,
                _tenantProvider.GetCurrentTenantId(),
                request.RoleIds ?? new List<Guid>()),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return MapFailure(result.Error!);
        }

        return NoContent();
    }

    private ObjectResult MapFailure(Error error) => error.Code switch
    {
        AuthErrorCodes.RoleNotFound or AuthErrorCodes.UserRoleNotFound => Problem(
            StatusCodes.Status404NotFound,
            _common.Text("NotFound"),
            _errors.Text(error.Code, error.Message),
            error.Code),
        AuthErrorCodes.SystemManagerImmutable or AuthErrorCodes.CrossTenantAssignmentDenied => Problem(
            StatusCodes.Status403Forbidden,
            _common.Text("Unauthorized"),
            _errors.Text(error.Code, error.Message),
            error.Code),
        AuthErrorCodes.RoleNameConflict
            or AuthErrorCodes.CannotRemoveLastAdmin
            or AuthErrorCodes.UserRoleAlreadyAssigned
            or AuthErrorCodes.RoleHasAssignments => Problem(
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
                StatusCodes.Status403Forbidden => "https://tools.ietf.org/html/rfc9110#section-15.5.4",
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

public sealed record CreateRoleRequest(string? Name, string? Description);

public sealed record UpdateRolePermissionsRequest(List<DocTypePermissionDto>? Permissions);

public sealed record AssignUserRolesRequest(List<Guid>? RoleIds);
