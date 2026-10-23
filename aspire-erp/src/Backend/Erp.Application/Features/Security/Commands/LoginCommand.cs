using Erp.Application.Common;
using Erp.Application.Services;
using Erp.Domain.Entities.Security;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Security.Commands;

public record LoginCommand(string Email, string Password, Guid? TenantId = null) : ICommand<Result<LoginResponseDto>>;

/// <summary>
/// Login outcome (spec 16 §3.1, plan Phase 1.1). Exactly one shape is ever populated:
/// <list type="bullet">
/// <item>Password-only accounts: <c>Token</c> carries the session JWT (<c>IsMfaRequired</c> false).</item>
/// <item>MFA-enabled accounts: NO JWT is issued; <c>IsMfaRequired</c> is true and
/// <c>MfaTicket</c> carries the short-lived password-proof ticket that <c>login-mfa</c>
/// redeems for the definitive JWT.</item>
/// </list>
/// Serialized camelCase (<c>token, fullName, email, isMfaRequired, mfaTicket</c>); pre-MFA
/// clients keep working because the first three properties are unchanged.
/// </summary>
public sealed record LoginResponseDto(
    string? Token,
    string FullName,
    string Email,
    bool IsMfaRequired,
    string? MfaTicket,
    IReadOnlyList<string>? Permissions = null)
{
    public static LoginResponseDto Authenticated(string token, string fullName, string email, IReadOnlyList<string>? permissions = null) =>
        new(token, fullName, email, IsMfaRequired: false, MfaTicket: null, Permissions: permissions);

    public static LoginResponseDto MfaChallenge(string fullName, string email, string ticket) =>
        new(Token: null, fullName, email, IsMfaRequired: true, MfaTicket: ticket, Permissions: null);
}

public class LoginCommandHandler : ICommandHandler<LoginCommand, Result<LoginResponseDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly ITokenGenerator _tokenGenerator;
    private readonly IPasswordHasher _passwords;
    private readonly IMfaChallengeTokenService _mfaTickets;

    public LoginCommandHandler(
        IUserRepository userRepository,
        ITokenGenerator tokenGenerator,
        IPasswordHasher passwords,
        IMfaChallengeTokenService mfaTickets)
    {
        _userRepository = userRepository;
        _tokenGenerator = tokenGenerator;
        _passwords = passwords;
        _mfaTickets = mfaTickets;
    }

    public async Task<Result<LoginResponseDto>> HandleAsync(LoginCommand request, CancellationToken cancellationToken = default)
    {
        // When TenantId is provided (e.g. from host subdomain or explicit header), query scoped to it.
        // Otherwise, resolve the user and their tenant directly by email.
        var user = request.TenantId.HasValue
            ? await _userRepository.GetUserByEmailAsync(request.Email, request.TenantId.Value, cancellationToken)
            : await _userRepository.GetUserByEmailAsync(request.Email, cancellationToken);

        if (user == null)
        {
            return Result<LoginResponseDto>.Failure("Auth.Failed", "Invalid credentials.");
        }

        // User Management (spec D2 USER-G4): a deactivated account is rejected before any
        // password verification, so a valid password can never mint a token for it.
        if (!user.IsActive)
        {
            return Result<LoginResponseDto>.Failure(
                AuthErrorCodes.AccountInactive,
                "This account is disabled. Contact an administrator.");
        }

        if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow)
        {
            return Result<LoginResponseDto>.Failure(
                AuthErrorCodes.AccountLocked,
                "The account is temporarily locked due to too many failed attempts.");
        }

        // PBKDF2 with legacy-plaintext fallback (module 15): pre-hashing imports keep working
        // and are upgraded to a hash on the next password change.
        if (!_passwords.Verify(request.Password, user.PasswordHash))
        {
            user.AccessFailedCount++;

            if (user.AccessFailedCount >= ProfileRules.MaxFailedAccessAttempts)
            {
                user.LockoutEnd = DateTimeOffset.UtcNow.Add(ProfileRules.LockoutDuration);
                await _userRepository.UpdateUserAsync(user, cancellationToken);

                return Result<LoginResponseDto>.Failure(
                    AuthErrorCodes.AccountLocked,
                    "The account is temporarily locked due to too many failed attempts.");
            }

            await _userRepository.UpdateUserAsync(user, cancellationToken);

            // Deliberately the legacy code/message: no user enumeration, no contract churn.
            return Result<LoginResponseDto>.Failure("Auth.Failed", "Invalid credentials.");
        }

        // Spec 16 §3.1: correct password but MFA on - stop here. No JWT, no counter reset
        // (the second factor is still unproven); the ticket binds this proof to step 2.
        if (user.TwoFactorEnabled)
        {
            string ticket = _mfaTickets.GenerateTicket(user);
            return Result<LoginResponseDto>.Success(
                LoginResponseDto.MfaChallenge(user.FullName, user.Email, ticket));
        }

        user.AccessFailedCount = 0;
        user.LockoutEnd = null;
        await _userRepository.UpdateUserAsync(user, cancellationToken);

        var permissions = await _userRepository.GetUserPermissionsAsync(user.Id, cancellationToken);
        var roles = user.UserRoles
            .Where(ur => ur.Role is not null)
            .Select(ur => ur.Role.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var token = _tokenGenerator.GenerateToken(user, permissions, roles);

        var permList = new List<string>();
        foreach (var p in permissions)
        {
            var dt = p.DocType.ToLowerInvariant().Replace(" ", "_");
            if (p.CanRead) permList.Add($"{dt}:read");
            if (p.CanWrite) permList.Add($"{dt}:write");
            if (p.CanCreate) permList.Add($"{dt}:create");
            if (p.CanDelete) permList.Add($"{dt}:delete");
            if (p.CanSubmit) permList.Add($"{dt}:submit");
            if (p.CanCancel) permList.Add($"{dt}:cancel");
        }

        return Result<LoginResponseDto>.Success(
            LoginResponseDto.Authenticated(token, user.FullName, user.Email, permList));
    }
}
