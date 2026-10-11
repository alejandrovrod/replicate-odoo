using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Erp.Api.Common;
using Erp.Api.Localization;
using Erp.Api.Shared;
using Erp.Application.Common;
using Erp.Application.Features.Security.Commands;
using Erp.Domain.Entities.Security;
using Erp.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Erp.Api.Controllers.V1;

/// <summary>
/// Self-service user profile &amp; security endpoints (module 15-user-profile, tasks.md Backend
/// item: HTTP POST endpoints for Password and MFA). Every mutation dispatches through the
/// hand-rolled <see cref="ISender"/> to <c>ICommandHandler&lt;,&gt;</c> implementations
/// (decision C2 - MediatR is prohibited); the caller identity always comes from the validated
/// JWT claims, never from the request body (no user enumeration, no cross-user writes).
/// </summary>
/// <remarks>
/// Domain error-code to status mapping (spec §4):
/// <list type="bullet">
/// <item><c>AUTH_INVALID_PASSWORD</c> -&gt; 401 (current password mismatch / unknown caller).</item>
/// <item><c>AUTH_ACCOUNT_LOCKED</c> -&gt; 423 (brute-force lockout in force).</item>
/// <item><c>AUTH_PASSWORD_POLICY_VIOLATION</c> -&gt; 400 (new password rejected).</item>
/// <item><c>MFA_INVALID_CODE</c> -&gt; 400 (wrong/expired TOTP code).</item>
/// <item><c>MFA_NOT_ENABLED</c> -&gt; 409 (action requires an active authenticator).</item>
/// </list>
/// The machine-readable <c>code</c> extension always carries the spec's Domain Error Code so
/// the SPA branches on it for i18n (see <c>useProfile.ts</c> + <c>error.json</c>).
/// </remarks>
[ApiController]
[Route("api/v1/profile")]
[Authorize(Policy = "TenantMember")]
[Produces("application/json")]
[Consumes("application/json")]
public sealed class ProfileController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IUserRepository _users;
    private readonly ITenantProvider _tenantProvider;
    private readonly IStringLocalizer<ErrorMessages> _errors;
    private readonly IStringLocalizer<CommonMessages> _common;

    public ProfileController(
        ISender sender,
        IUserRepository users,
        ITenantProvider tenantProvider,
        IStringLocalizer<ErrorMessages> errors,
        IStringLocalizer<CommonMessages> common)
    {
        _sender = sender;
        _users = users;
        _tenantProvider = tenantProvider;
        _errors = errors;
        _common = common;
    }

    /// <summary>Returns the caller's profile (display name, email, MFA status).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        if (!TryGetCaller(out Guid userId, out _))
        {
            return UnauthorizedProblem(AuthErrorCodes.InvalidPassword);
        }

        var user = await _users.GetUserByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return UnauthorizedProblem(AuthErrorCodes.InvalidPassword);
        }

        return Ok(ProfileDto.From(user));
    }

    /// <summary>
    /// Changes the caller's password (spec §3.1). A wrong current password increments
    /// <c>AccessFailedCount</c> and can lock the account (423).
    /// </summary>
    [HttpPost("change-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCaller(out Guid userId, out Guid tenantId))
        {
            return UnauthorizedProblem(AuthErrorCodes.InvalidPassword);
        }

        var result = await _sender.SendAsync(
            new ChangePasswordCommand(
                userId,
                tenantId,
                request.CurrentPassword ?? string.Empty,
                request.NewPassword ?? string.Empty,
                request.ConfirmPassword ?? string.Empty),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return MapFailure(result.Error!);
        }

        return NoContent();
    }

    /// <summary>
    /// Stages MFA enrollment (spec §3.2): returns the TOTP URI (QR material) and the initial
    /// plaintext backup codes, which the SPA must force the user to download/copy BEFORE
    /// calling verify. Enrollment is inactive until verified.
    /// </summary>
    [HttpPost("mfa/enable")]
    [ProducesResponseType(typeof(EnableMfaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> EnableMfa(CancellationToken cancellationToken)
    {
        if (!TryGetCaller(out Guid userId, out Guid tenantId))
        {
            return UnauthorizedProblem(AuthErrorCodes.InvalidPassword);
        }

        var result = await _sender.SendAsync(new EnableMfaCommand(userId, tenantId), cancellationToken);
        if (!result.IsSuccess)
        {
            return MapFailure(result.Error!);
        }

        var value = result.Value!;
        return Ok(new EnableMfaResponse(value.SharedKey, value.AuthenticatorUri, value.RecoveryCodes));
    }

    /// <summary>Finalizes MFA enrollment by verifying the 6-digit TOTP code (spec §3.2).</summary>
    [HttpPost("mfa/verify")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> VerifyMfa(
        [FromBody] VerifyMfaRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCaller(out Guid userId, out Guid tenantId))
        {
            return UnauthorizedProblem(AuthErrorCodes.InvalidPassword);
        }

        var result = await _sender.SendAsync(
            new VerifyMfaCommand(userId, tenantId, request.Code ?? string.Empty),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return MapFailure(result.Error!);
        }

        return NoContent();
    }

    /// <summary>
    /// Issues 10 fresh backup codes, invalidating previously issued ones (spec §3.2).
    /// Requires an active authenticator (409 + <c>MFA_NOT_ENABLED</c> otherwise).
    /// </summary>
    [HttpPost("mfa/backup-codes")]
    [ProducesResponseType(typeof(BackupCodesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GenerateBackupCodes(CancellationToken cancellationToken)
    {
        if (!TryGetCaller(out Guid userId, out Guid tenantId))
        {
            return UnauthorizedProblem(AuthErrorCodes.InvalidPassword);
        }

        var result = await _sender.SendAsync(new GenerateBackupCodesCommand(userId, tenantId), cancellationToken);
        if (!result.IsSuccess)
        {
            return MapFailure(result.Error!);
        }

        return Ok(new BackupCodesResponse(result.Value!));
    }

    /// <summary>Turns MFA off after proving possession with a current TOTP code.</summary>
    [HttpPost("mfa/disable")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DisableMfa(
        [FromBody] VerifyMfaRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCaller(out Guid userId, out Guid tenantId))
        {
            return UnauthorizedProblem(AuthErrorCodes.InvalidPassword);
        }

        var result = await _sender.SendAsync(
            new DisableMfaCommand(userId, tenantId, request.Code ?? string.Empty),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return MapFailure(result.Error!);
        }

        return NoContent();
    }

    /// <summary>
    /// Resolves the caller from the validated JWT (<c>sub</c> = user id) plus the resolved
    /// tenant. False when the token carries no usable identity.
    /// </summary>
    private bool TryGetCaller(out Guid userId, out Guid tenantId)
    {
        userId = Guid.Empty;
        tenantId = _tenantProvider.HasTenant() ? _tenantProvider.GetCurrentTenantId() : Guid.Empty;

        string? sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        string? tenantClaim = User.FindFirstValue("tenant_id");

        if (!Guid.TryParse(sub, out userId))
        {
            userId = Guid.Empty;
            return false;
        }

        if (Guid.TryParse(tenantClaim, out Guid claimedTenant))
        {
            tenantId = claimedTenant;
        }

        return tenantId != Guid.Empty;
    }

    private ObjectResult MapFailure(Error error) => error.Code switch
    {
        AuthErrorCodes.InvalidPassword => Problem(
            StatusCodes.Status401Unauthorized,
            _common.Text("Unauthorized"),
            _errors.Text(error.Code, error.Message),
            error.Code),
        AuthErrorCodes.AccountLocked => Problem(
            StatusCodes.Status423Locked,
            _common.Text("AccountLocked"),
            _errors.Text(error.Code, error.Message),
            error.Code),
        AuthErrorCodes.MfaNotEnabled => Problem(
            StatusCodes.Status409Conflict,
            _common.Text("Conflict"),
            _errors.Text(error.Code, error.Message),
            error.Code),
        _ => Problem(
            StatusCodes.Status400BadRequest,
            _common.Text("ProfileRejected"),
            _errors.Text(error.Code, error.Message),
            error.Code),
    };

    private ObjectResult UnauthorizedProblem(string code) =>
        Problem(
            StatusCodes.Status401Unauthorized,
            _common.Text("Unauthorized"),
            _errors.Text(code),
            code);

    private ObjectResult Problem(int status, string title, string detail, string? code)
    {
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
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

public sealed record ChangePasswordRequest(
    string? CurrentPassword,
    string? NewPassword,
    string? ConfirmPassword);

public sealed record VerifyMfaRequest(string? Code);

public sealed record ProfileDto(string Email, string FullName, bool TwoFactorEnabled)
{
    public static ProfileDto From(Erp.Domain.Entities.Security.User user) =>
        new(user.Email, user.FullName, user.TwoFactorEnabled);
}

public sealed record EnableMfaResponse(
    string SharedKey,
    string AuthenticatorUri,
    IReadOnlyList<string> RecoveryCodes);

public sealed record BackupCodesResponse(IReadOnlyList<string> RecoveryCodes);
