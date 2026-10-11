using Erp.Application.Features.Security.Commands;
using Erp.Application.Services;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities.Security;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Module 15-user-profile (tasks.md Backend item): MFA handlers cover staged enrollment,
/// TOTP verification (success + <c>MFA_INVALID_CODE</c>), the backup-codes gate
/// (<c>MFA_NOT_ENABLED</c>) with invalidation-on-regeneration, and disable.
/// </summary>
public sealed class MfaCommandHandlerTests
{
    private readonly FakeUserRepository _users = new();
    private readonly TotpService _totp = new();
    private readonly RecoveryCodeGenerator _recoveryCodes = new();

    private User SeedUser()
    {
        var user = FakeUserRepository.WithPassword();
        _users.Seed(user);
        return user;
    }

    private string CurrentCode(string secret) =>
        _totp.ComputeCode(secret, DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30);

    [Fact]
    public async Task Enable_StagesSecretWithoutActivating_AndIssuesTenCodes()
    {
        var user = SeedUser();

        var result = await new EnableMfaCommandHandler(_users, _totp, _recoveryCodes)
            .HandleAsync(new EnableMfaCommand(user.Id, user.TenantId));

        Assert.True(result.IsSuccess);
        Assert.False(user.TwoFactorEnabled); // verify() flips this, not enable()
        Assert.NotNull(user.AuthenticatorKey);
        Assert.StartsWith("otpauth://totp/", result.Value!.AuthenticatorUri);
        Assert.Equal(ProfileRules.RecoveryCodeCount, result.Value.RecoveryCodes.Count);

        // Only hashes are persisted - no plaintext recovery code may rest in the store.
        var stored = await _users.GetActiveRecoveryCodesAsync(user.Id);
        Assert.Equal(ProfileRules.RecoveryCodeCount, stored.Count);
        foreach (var row in stored)
        {
            Assert.DoesNotContain("-", row.CodeHash);
            Assert.Equal(64, row.CodeHash.Length);
            Assert.Contains(result.Value.RecoveryCodes, plain => _recoveryCodes.Hash(plain) == row.CodeHash);
        }
    }

    [Fact]
    public async Task Verify_WrongCode_ReturnsMfaInvalidCode()
    {
        var user = SeedUser();
        var enable = await new EnableMfaCommandHandler(_users, _totp, _recoveryCodes)
            .HandleAsync(new EnableMfaCommand(user.Id, user.TenantId));
        Assert.True(enable.IsSuccess);

        var result = await new VerifyMfaCommandHandler(_users, _totp)
            .HandleAsync(new VerifyMfaCommand(user.Id, user.TenantId, "000000"));

        // "000000" could theoretically be the live code once in a million enrollments; retry
        // with a neighbor when the universe conspires against the assertion.
        if (result.IsSuccess)
        {
            result = await new VerifyMfaCommandHandler(_users, _totp)
                .HandleAsync(new VerifyMfaCommand(user.Id, user.TenantId, "000001"));
        }

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrorCodes.MfaInvalidCode, result.Error!.Code);
        Assert.False(user.TwoFactorEnabled);
    }

    [Fact]
    public async Task Verify_CorrectCode_ActivatesMfa()
    {
        var user = SeedUser();
        var enable = await new EnableMfaCommandHandler(_users, _totp, _recoveryCodes)
            .HandleAsync(new EnableMfaCommand(user.Id, user.TenantId));
        Assert.True(enable.IsSuccess);

        var result = await new VerifyMfaCommandHandler(_users, _totp)
            .HandleAsync(new VerifyMfaCommand(user.Id, user.TenantId, CurrentCode(user.AuthenticatorKey!)));

        Assert.True(result.IsSuccess);
        Assert.True(user.TwoFactorEnabled);
    }

    [Fact]
    public async Task GenerateBackupCodes_WhenMfaOff_ReturnsMfaNotEnabled()
    {
        var user = SeedUser();

        var result = await new GenerateBackupCodesCommandHandler(_users, _recoveryCodes)
            .HandleAsync(new GenerateBackupCodesCommand(user.Id, user.TenantId));

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrorCodes.MfaNotEnabled, result.Error!.Code);
    }

    [Fact]
    public async Task GenerateBackupCodes_WhenMfaOn_InvalidatesPreviousCodes()
    {
        var user = SeedUser();
        user.TwoFactorEnabled = true;
        var first = await new GenerateBackupCodesCommandHandler(_users, _recoveryCodes)
            .HandleAsync(new GenerateBackupCodesCommand(user.Id, user.TenantId));
        Assert.True(first.IsSuccess);

        var second = await new GenerateBackupCodesCommandHandler(_users, _recoveryCodes)
            .HandleAsync(new GenerateBackupCodesCommand(user.Id, user.TenantId));
        Assert.True(second.IsSuccess);

        var stored = await _users.GetActiveRecoveryCodesAsync(user.Id);
        Assert.Equal(ProfileRules.RecoveryCodeCount, stored.Count);
        var storedHashes = stored.Select(c => c.CodeHash).ToHashSet(StringComparer.Ordinal);
        // Every previously issued code is dead: none of its hashes survives the replace.
        Assert.DoesNotContain(first.Value!, plain => storedHashes.Contains(_recoveryCodes.Hash(plain)));
        Assert.Equal(
            ProfileRules.RecoveryCodeCount,
            second.Value!.Count(plain => storedHashes.Contains(_recoveryCodes.Hash(plain))));
    }

    [Fact]
    public async Task Disable_WithValidCode_ClearsSecretFlagAndCodes()
    {
        var user = SeedUser();
        var enable = await new EnableMfaCommandHandler(_users, _totp, _recoveryCodes)
            .HandleAsync(new EnableMfaCommand(user.Id, user.TenantId));
        Assert.True(enable.IsSuccess);
        var verified = await new VerifyMfaCommandHandler(_users, _totp)
            .HandleAsync(new VerifyMfaCommand(user.Id, user.TenantId, CurrentCode(user.AuthenticatorKey!)));
        Assert.True(verified.IsSuccess);

        var result = await new DisableMfaCommandHandler(_users, _totp)
            .HandleAsync(new DisableMfaCommand(user.Id, user.TenantId, CurrentCode(user.AuthenticatorKey!)));

        Assert.True(result.IsSuccess);
        Assert.False(user.TwoFactorEnabled);
        Assert.Null(user.AuthenticatorKey);
        Assert.Empty(await _users.GetActiveRecoveryCodesAsync(user.Id));
    }
}
