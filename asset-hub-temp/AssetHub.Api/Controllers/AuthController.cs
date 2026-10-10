using System.Threading.Tasks;
using AssetHub.Application.Auth.Commands;
using AssetHub.Application.Auth.Mfa;
using AssetHub.Application.Auth.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetHub.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _mediator.Send(new LoginCommand(request.Email, request.Password, request.MfaCode));
        return Ok(result);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        var result = await _mediator.Send(new RefreshCommand(request.RefreshToken));
        return Ok(result);
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterViaInvitationCommand request)
    {
        var result = await _mediator.Send(request);
        return Ok(new { success = result });
    }

    [HttpPost("signup-tenant")]
    public async Task<IActionResult> SignUpTenant([FromBody] AssetHub.Application.Tenancy.Commands.SignUpTenantCommand request)
    {
        var result = await _mediator.Send(request);
        return Ok(result);
    }

    /// <summary>
    /// Valida un token de invitación (público: lo usa la pantalla de
    /// aceptación antes de login para mostrar estado del link).
    /// </summary>
    [HttpGet("invitations/{token}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetInvitationByToken(string token)
    {
        var result = await _mediator.Send(
            new AssetHub.Application.Users.Queries.GetInvitationByTokenQuery { Token = token });
        if (result == null) return NotFound();
        return Ok(result);
    }

    /// <summary>
    /// Obtiene el perfil del usuario autenticado actual.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetMe()
    {
        var result = await _mediator.Send(new GetCurrentUserQuery());
        if (result == null) return NotFound();
        return Ok(result);
    }

    /// <summary>
    /// Actualiza el perfil del usuario autenticado (nombre, email, locale).
    /// </summary>
    [HttpPut("me")]
    [Authorize]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateMeRequest request)
    {
        var result = await _mediator.Send(new UpdateCurrentUserCommand(
            request.FullName,
            request.Email,
            request.PreferredLocale
        ));
        return Ok(result);
    }

    /// <summary>
    /// Cambia la contraseña del usuario autenticado.
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        await _mediator.Send(new ChangePasswordCommand
        {
            CurrentPassword = request.CurrentPassword,
            NewPassword = request.NewPassword,
            ConfirmPassword = request.ConfirmPassword
        });
        return Ok(new { success = true });
    }

    /// <summary>
    /// Obtiene el estado de MFA del usuario autenticado.
    /// </summary>
    [HttpGet("me/mfa")]
    [Authorize]
    public async Task<IActionResult> GetMfaStatus()
    {
        var result = await _mediator.Send(new GetMfaStatusQuery());
        return Ok(result);
    }

    /// <summary>
    /// Inicia la configuración de MFA (genera secret, QR code y backup codes).
    /// </summary>
    [HttpPost("me/mfa/setup")]
    [Authorize]
    public async Task<IActionResult> InitiateMfaSetup()
    {
        var result = await _mediator.Send(new InitiateMfaSetupCommand());
        return Ok(result);
    }

    /// <summary>
    /// Verifica el código TOTP y habilita MFA.
    /// </summary>
    [HttpPost("me/mfa/verify")]
    [Authorize]
    public async Task<IActionResult> VerifyMfaSetup([FromBody] VerifyMfaRequest request)
    {
        await _mediator.Send(new VerifyMfaSetupCommand(request.TotpCode));
        return Ok(new { success = true });
    }

    /// <summary>
    /// Deshabilita MFA (requiere contraseña).
    /// </summary>
    [HttpPost("me/mfa/disable")]
    [Authorize]
    public async Task<IActionResult> DisableMfa([FromBody] DisableMfaRequest request)
    {
        await _mediator.Send(new DisableMfaCommand(request.Password));
        return Ok(new { success = true });
    }

    /// <summary>
    /// Obtiene los códigos de recuperación actuales.
    /// </summary>
    [HttpGet("me/mfa/backup-codes")]
    [Authorize]
    public async Task<IActionResult> GetBackupCodes()
    {
        var result = await _mediator.Send(new GetBackupCodesCommand());
        return Ok(result);
    }

    /// <summary>
    /// Regenera los códigos de recuperación.
    /// </summary>
    [HttpPost("me/mfa/backup-codes/regenerate")]
    [Authorize]
    public async Task<IActionResult> RegenerateBackupCodes()
    {
        var result = await _mediator.Send(new RegenerateBackupCodesCommand());
        return Ok(result);
    }
}

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? MfaCode { get; set; }
}

public class RefreshRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

public class UpdateMeRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PreferredLocale { get; set; }
}

public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class VerifyMfaRequest
{
    public string TotpCode { get; set; } = string.Empty;
}

public class DisableMfaRequest
{
    public string Password { get; set; } = string.Empty;
}
