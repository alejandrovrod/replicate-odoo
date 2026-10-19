using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Erp.Api.Common;
using Erp.Api.Localization;
using Erp.Api.Shared;
using Erp.Application.Common;
using Erp.Application.Features.Security.Users;
using Erp.Domain.Common;
using Erp.Domain.Entities.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Erp.Api.Controllers.V1;

/// <summary>
/// User Management endpoints (user-management spec D2/D4). Administrators list, create, edit,
/// activate/deactivate and reset passwords for users of the current tenant only. Every mutation
/// dispatches through the hand-rolled <see cref="ISender"/> (decision C2).
/// </summary>
/// <remarks>
/// Authorization is server-side: <c>TenantMember</c> (tenant resolved) AND the
/// <c>permission:user:*</c> requirement. The dynamic provider grants it to System Manager or to
/// a role holding the matching DocType permission (<c>user</c> is registered in
/// <see cref="Erp.Application.Features.Security.Roles.DocTypeCatalog"/>).
/// <para>
/// Tenant isolation is enforced below the controller by the global query filter: a user of
/// another tenant is <c>404 USER_NOT_FOUND</c>, and the same email is legal in another tenant.
/// </para>
/// <para>Domain error-code to status mapping (spec D4):</para>
/// <list type="bullet">
/// <item><c>USER_NOT_FOUND</c> -&gt; 404.</item>
/// <item><c>USER_EMAIL_CONFLICT / CANNOT_REMOVE_LAST_ADMIN / STALE_ROW_VERSION</c> -&gt; 409.</item>
/// <item><c>INVALID_USER_DATA</c> and any other validation failure -&gt; 400.</item>
/// </list>
/// </remarks>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "TenantMember")]
[Produces("application/json")]
[Consumes("application/json")]
public sealed class UsersController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ITenantProvider _tenantProvider;
    private readonly IStringLocalizer<ErrorMessages> _errors;
    private readonly IStringLocalizer<CommonMessages> _common;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        ISender sender,
        ITenantProvider tenantProvider,
        IStringLocalizer<ErrorMessages> errors,
        IStringLocalizer<CommonMessages> common,
        ILogger<UsersController> logger)
    {
        _sender = sender;
        _tenantProvider = tenantProvider;
        _errors = errors;
        _common = common;
        _logger = logger;
    }

    /// <summary>
    /// One page of the current tenant's users. <paramref name="search"/> matches email or full
    /// name (USER-G1). Never exposes password hashes.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "permission:user:read")]
    [ProducesResponseType(typeof(PagedResult<UserAdminDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var users = await _sender.SendAsync(
            new GetUsersPagedQuery(page, pageSize, search),
            cancellationToken);

        return Ok(users);
    }

    /// <summary>Single user with role assignments (edit dialog).</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "permission:user:read")]
    [ProducesResponseType(typeof(UserAdminDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUser(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(new GetUserByIdQuery(id), cancellationToken);

        return result.IsSuccess ? Ok(result.Value!) : MapFailure(result.Error!);
    }

    /// <summary>
    /// Creates a tenant user (USER-G2). Returns 201 with the user and the temporary password
    /// (generated server-side when the admin left the field empty) so it can be handed over once.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "permission:user:create")]
    [ProducesResponseType(typeof(CreateUserResult), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateUser(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(
            new CreateUserCommand(
                request.FullName ?? string.Empty,
                request.Email ?? string.Empty,
                request.TemporaryPassword,
                request.RoleIds ?? new List<Guid>(),
                request.IsActive),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return MapFailure(result.Error!);
        }

        var created = result.Value!;
        var dto = created.User;
        UserAuditLog.UserCreated(
            _logger,
            GetActorId(),
            _tenantProvider.GetCurrentTenantId(),
            dto.Id,
            dto.Email,
            dto.IsActive,
            dto.RoleIds.Count);

        return CreatedAtAction(nameof(GetUser), new { id = dto.Id }, created);
    }

    /// <summary>
    /// Edits name, roles and lifecycle (USER-G3) under optimistic concurrency. A token that no
    /// longer matches is <c>409 STALE_ROW_VERSION</c>; deactivating or demoting the last active
    /// System Manager is <c>409 CANNOT_REMOVE_LAST_ADMIN</c>.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "permission:user:write")]
    [ProducesResponseType(typeof(UserAdminDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateUser(
        Guid id,
        [FromBody] UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(
            new UpdateUserCommand(
                id,
                request.FullName ?? string.Empty,
                request.RoleIds ?? new List<Guid>(),
                request.IsActive,
                request.RowVersion),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return MapFailure(result.Error!);
        }

        var dto = result.Value!;
        UserAuditLog.UserUpdated(
            _logger,
            GetActorId(),
            _tenantProvider.GetCurrentTenantId(),
            dto.Id,
            dto.IsActive,
            dto.RoleIds.Count);

        return Ok(dto);
    }

    /// <summary>
    /// Activates or deactivates a user (USER-G4). Deactivating the tenant's last active System
    /// Manager is <c>409 CANNOT_REMOVE_LAST_ADMIN</c>; repeating the current state is a no-op.
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = "permission:user:write")]
    [ProducesResponseType(typeof(UserAdminDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SetStatus(
        Guid id,
        [FromBody] SetUserStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(
            new ToggleUserStatusCommand(id, request.IsActive),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return MapFailure(result.Error!);
        }

        var dto = result.Value!;
        UserAuditLog.UserStatusChanged(
            _logger,
            GetActorId(),
            _tenantProvider.GetCurrentTenantId(),
            dto.Id,
            dto.IsActive);

        return Ok(dto);
    }

    /// <summary>
    /// Administrative password reset (USER-G5): assigns the supplied temporary password or
    /// generates a secure one, clears lockout and returns the plaintext exactly once.
    /// </summary>
    [HttpPost("{id:guid}/reset-password")]
    [Authorize(Policy = "permission:user:write")]
    [ProducesResponseType(typeof(ResetUserPasswordResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetPassword(
        Guid id,
        [FromBody] ResetUserPasswordRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(
            new ResetUserPasswordCommand(id, request?.TemporaryPassword),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return MapFailure(result.Error!);
        }

        UserAuditLog.UserPasswordReset(
            _logger,
            GetActorId(),
            _tenantProvider.GetCurrentTenantId(),
            id);

        return Ok(result.Value!);
    }

    private ObjectResult MapFailure(Error error) => error.Code switch
    {
        AuthErrorCodes.UserNotFound => Problem(
            StatusCodes.Status404NotFound,
            _common.Text("NotFound"),
            _errors.Text(error.Code, error.Message),
            error.Code),
        AuthErrorCodes.UserEmailConflict
            or AuthErrorCodes.CannotRemoveLastAdmin
            or AuthErrorCodes.StaleRowVersion => Problem(
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

    /// <summary>Caller id for the audit trail; empty when the token has no usable subject.</summary>
    private Guid GetActorId()
    {
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(sub, out var actorId) ? actorId : Guid.Empty;
    }
}

/// <summary>Create payload (spec D4). Email is normalized (trim + lowercase) server-side.</summary>
public sealed record CreateUserRequest(
    string? FullName,
    string? Email,
    string? TemporaryPassword,
    List<Guid>? RoleIds,
    bool IsActive = true);

/// <summary>
/// Edit payload (spec D4). Email is intentionally absent: it is immutable for administrators.
/// <c>IsActive</c> is nullable so clients that do not manage status preserve the current value.
/// </summary>
public sealed record UpdateUserRequest(
    string? FullName,
    List<Guid>? RoleIds,
    bool? IsActive,
    byte[]? RowVersion);

/// <summary>Activate/deactivate payload (USER-G4).</summary>
public sealed record SetUserStatusRequest(bool IsActive);

/// <summary>Optional reset payload; omitting it generates a secure temporary password.</summary>
public sealed record ResetUserPasswordRequest(string? TemporaryPassword);
