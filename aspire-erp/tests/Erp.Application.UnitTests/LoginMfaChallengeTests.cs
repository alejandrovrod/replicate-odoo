using Erp.Application.Common;
using Erp.Application.Features.Security.Commands;
using Erp.Application.Services;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Erp.Domain.Entities.Security;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Module 16-auth-login-mfa: step 1 withholds the JWT when MFA is on (returning the
/// password-proof ticket instead), and step 2 redeems it with a TOTP or single-use backup
/// code - failures feed the brute-force counter toward lockout, success issues the JWT.
/// </summary>
public sealed class LoginMfaChallengeTests
{
    private readonly FakeUserRepository _users = new();
    private readonly PasswordHasher _passwords = new();
    private readonly TotpService _totp = new();
    private readonly RecoveryCodeGenerator _recoveryCodes = new();
    private readonly StubTicketService _tickets = new();
    private readonly StubTokenGenerator _tokens = new();

    private const string Password = "Secret123!";

    /// <summary>Deterministic ticket stand-in: "ticket:{tenant}:{user}". Anything else is invalid.</summary>
    private sealed class StubTicketService : IMfaChallengeTokenService
    {
        public string GenerateTicket(User user) => $"ticket:{user.TenantId}:{user.Id}";

        public bool TryValidateTicket(string? ticket, out Guid userId, out Guid tenantId)
        {
            userId = Guid.Empty;
            tenantId = Guid.Empty;
            var parts = (ticket ?? string.Empty).Split(':');
            return parts.Length == 3
                && parts[0] == "ticket"
                && Guid.TryParse(parts[1], out tenantId)
                && tenantId != Guid.Empty
                && Guid.TryParse(parts[2], out userId)
                && userId != Guid.Empty;
        }
    }

    private sealed class StubTokenGenerator : ITokenGenerator
    {
        public string GenerateToken(User user, IEnumerable<DocTypePermission> permissions, IEnumerable<string>? roles = null)
            => $"jwt-{user.Id}";
    }

    private User SeedUser(bool mfaEnabled = false, string? authenticatorKey = null)
    {
        var user = FakeUserRepository.WithPassword(passwordHash: _passwords.Hash(Password));
        user.TwoFactorEnabled = mfaEnabled;
        user.AuthenticatorKey = authenticatorKey;
        _users.Seed(user);
        return user;
    }

    private string SeedMfaUser(out User user)
    {
        var secret = _totp.GenerateSecret();
        user = SeedUser(mfaEnabled: true, authenticatorKey: secret);
        return secret;
    }

    private void SeedBackupCodes(User user, IReadOnlyList<string> plaintext)
    {
        _users.ReplaceRecoveryCodesAsync(
            user,
            plaintext.Select(code => new UserRecoveryCode
            {
                Id = Guid.NewGuid(),
                TenantId = user.TenantId,
                UserId = user.Id,
                CodeHash = _recoveryCodes.Hash(code),
                CreatedAt = DateTimeOffset.UtcNow,
            }).ToList()).GetAwaiter().GetResult();
    }

    private string CurrentCode(string secret) =>
        _totp.ComputeCode(secret, DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30);

    private LoginCommandHandler LoginHandler() =>
        new(_users, _tokens, _passwords, _tickets);

    private VerifyLoginMfaCommandHandler VerifyHandler() =>
        new(_users, _tokens, _totp, _recoveryCodes, _tickets);

    // ------------------------------------------------------------------ step 1 (login)

    [Fact]
    public async Task Login_PasswordOkWithoutMfa_IssuesJwtDirectly()
    {
        var user = SeedUser();

        var result = await LoginHandler().HandleAsync(new LoginCommand(user.Email, Password, user.TenantId));

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.IsMfaRequired);
        Assert.Equal($"jwt-{user.Id}", result.Value.Token);
        Assert.Null(result.Value.MfaTicket);
        Assert.Equal(0, user.AccessFailedCount);
    }

    [Fact]
    public async Task Login_PasswordOkWithMfa_WithholdsJwtAndReturnsTicket()
    {
        var secret = _totp.GenerateSecret();
        var user = SeedUser(mfaEnabled: true, authenticatorKey: secret);

        var result = await LoginHandler().HandleAsync(new LoginCommand(user.Email, Password, user.TenantId));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsMfaRequired);
        Assert.Null(result.Value.Token);
        Assert.Equal($"ticket:{user.TenantId}:{user.Id}", result.Value.MfaTicket);
        Assert.Equal(user.Email, result.Value.Email);
    }

    [Fact]
    public async Task Login_WrongPassword_KeepsLegacyFailureAndCountsAttempt()
    {
        var user = SeedUser();

        var result = await LoginHandler().HandleAsync(new LoginCommand(user.Email, "Wrong123!", user.TenantId));

        Assert.False(result.IsSuccess);
        Assert.Equal("Auth.Failed", result.Error!.Code);
        Assert.Equal(1, user.AccessFailedCount);
    }

    [Fact]
    public async Task Login_WhenLocked_RejectsEvenCorrectPassword()
    {
        var user = SeedUser();
        user.AccessFailedCount = ProfileRules.MaxFailedAccessAttempts;
        user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(10);

        var result = await LoginHandler().HandleAsync(new LoginCommand(user.Email, Password, user.TenantId));

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrorCodes.AccountLocked, result.Error!.Code);
    }

    // ------------------------------------------------------------------ step 2 (login-mfa)

    [Fact]
    public async Task VerifyLoginMfa_ValidTotp_IssuesJwtAndResetsCounter()
    {
        var secret = SeedMfaUser(out var user);
        user.AccessFailedCount = 2;

        var result = await VerifyHandler().HandleAsync(
            new VerifyLoginMfaCommand($"ticket:{user.TenantId}:{user.Id}", CurrentCode(secret), user.TenantId));

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.IsMfaRequired);
        Assert.Equal($"jwt-{user.Id}", result.Value.Token);
        Assert.Equal(0, user.AccessFailedCount);
        Assert.Null(user.LockoutEnd);
    }

    [Fact]
    public async Task VerifyLoginMfa_ValidBackupCode_IssuesJwtAndBurnsCode()
    {
        SeedMfaUser(out var user);
        var codes = _recoveryCodes.Generate(ProfileRules.RecoveryCodeCount);
        SeedBackupCodes(user, codes);

        var first = await VerifyHandler().HandleAsync(
            new VerifyLoginMfaCommand($"ticket:{user.TenantId}:{user.Id}", codes[0], user.TenantId));

        Assert.True(first.IsSuccess);
        Assert.Equal($"jwt-{user.Id}", first.Value!.Token);

        // Single-use: the same code is dead on the next challenge.
        var replay = await VerifyHandler().HandleAsync(
            new VerifyLoginMfaCommand($"ticket:{user.TenantId}:{user.Id}", codes[0], user.TenantId));

        Assert.False(replay.IsSuccess);
        Assert.Equal(AuthErrorCodes.MfaInvalidLoginCode, replay.Error!.Code);
    }

    [Fact]
    public async Task VerifyLoginMfa_WrongCode_CountsAttemptWithoutLocking()
    {
        SeedMfaUser(out var user);

        var result = await VerifyHandler().HandleAsync(
            new VerifyLoginMfaCommand($"ticket:{user.TenantId}:{user.Id}", "123456", user.TenantId));

        // "123456" could theoretically be the live code once in a million runs; retry once.
        if (result.IsSuccess)
        {
            result = await VerifyHandler().HandleAsync(
                new VerifyLoginMfaCommand($"ticket:{user.TenantId}:{user.Id}", "123457", user.TenantId));
        }

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrorCodes.MfaInvalidLoginCode, result.Error!.Code);
        Assert.Equal(1, user.AccessFailedCount);
    }

    [Fact]
    public async Task VerifyLoginMfa_RepeatedFailures_LockAccount()
    {
        SeedMfaUser(out var user);
        user.AccessFailedCount = ProfileRules.MaxFailedAccessAttempts - 1;

        var result = await VerifyHandler().HandleAsync(
            new VerifyLoginMfaCommand($"ticket:{user.TenantId}:{user.Id}", "999999", user.TenantId));

        if (result.IsSuccess)
        {
            // Won the TOTP lottery; the counter assertion below still holds on retry.
            user.AccessFailedCount = ProfileRules.MaxFailedAccessAttempts - 1;
            user.LockoutEnd = null;
            result = await VerifyHandler().HandleAsync(
                new VerifyLoginMfaCommand($"ticket:{user.TenantId}:{user.Id}", "999998", user.TenantId));
        }

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrorCodes.AccountLocked, result.Error!.Code);
        Assert.NotNull(user.LockoutEnd);
    }

    [Fact]
    public async Task VerifyLoginMfa_InvalidTicket_FailsWithoutTouchingCounter()
    {
        SeedMfaUser(out var user);

        var result = await VerifyHandler().HandleAsync(
            new VerifyLoginMfaCommand("not-a-ticket", "123456", user.TenantId));

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrorCodes.MfaInvalidLoginCode, result.Error!.Code);
        Assert.Equal(0, user.AccessFailedCount);
    }

    [Fact]
    public async Task VerifyLoginMfa_TicketFromAnotherTenant_Fails()
    {
        SeedMfaUser(out var user);

        var result = await VerifyHandler().HandleAsync(
            new VerifyLoginMfaCommand($"ticket:{user.TenantId}:{user.Id}", "123456", Guid.NewGuid()));

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrorCodes.MfaInvalidLoginCode, result.Error!.Code);
    }

    [Fact]
    public async Task VerifyLoginMfa_MfaDisabledAfterTicket_IssuesJwtGracefully()
    {
        var secret = SeedMfaUser(out var user);
        user.TwoFactorEnabled = false;
        user.AuthenticatorKey = null;

        var result = await VerifyHandler().HandleAsync(
            new VerifyLoginMfaCommand($"ticket:{user.TenantId}:{user.Id}", CurrentCode(secret), user.TenantId));

        Assert.True(result.IsSuccess);
        Assert.Equal($"jwt-{user.Id}", result.Value!.Token);
    }
}
