using Erp.Application.Features.Security.Commands;
using Erp.Application.Services;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities.Security;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Module 15-user-profile (tasks.md Backend item): <c>ChangePasswordCommandHandler</c> covers
/// success, wrong password (brute-force counter) and the lockout threshold, plus the
/// password-policy gate and the unknown-user wire shape.
/// </summary>
public sealed class ChangePasswordCommandHandlerTests
{
    private readonly FakeUserRepository _users = new();
    private readonly PasswordHasher _passwords = new();

    private ChangePasswordCommandHandler CreateHandler() => new(_users, _passwords);

    private User SeedUser(string currentPassword, int failedCount = 0, DateTimeOffset? lockoutEnd = null)
    {
        var user = FakeUserRepository.WithPassword(passwordHash: _passwords.Hash(currentPassword));
        user.AccessFailedCount = failedCount;
        user.LockoutEnd = lockoutEnd;
        _users.Seed(user);
        return user;
    }

    private static ChangePasswordCommand Command(Guid userId, Guid tenantId, string current, string next = "NewPassword123") =>
        new(userId, tenantId, current, next, next);

    [Fact]
    public async Task HandleAsync_ValidCurrentPassword_ChangesHashAndResetsCounter()
    {
        var user = SeedUser("OldPassword123", failedCount: 2);
        var before = user.PasswordHash;

        var result = await CreateHandler().HandleAsync(Command(user.Id, user.TenantId, "OldPassword123"));

        Assert.True(result.IsSuccess);
        Assert.NotEqual(before, user.PasswordHash);
        Assert.True(_passwords.Verify("NewPassword123", user.PasswordHash));
        Assert.Equal(0, user.AccessFailedCount);
        Assert.Null(user.LockoutEnd);
    }

    [Fact]
    public async Task HandleAsync_WrongPassword_ReturnsInvalidPasswordAndIncrementsCounter()
    {
        var user = SeedUser("OldPassword123");

        var result = await CreateHandler().HandleAsync(Command(user.Id, user.TenantId, "NotThePassword1"));

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrorCodes.InvalidPassword, result.Error!.Code);
        Assert.Equal(1, user.AccessFailedCount);
        Assert.Null(user.LockoutEnd);
        // The stored hash is untouched: the attacker learned nothing reusable.
        Assert.True(_passwords.Verify("OldPassword123", user.PasswordHash));
    }

    [Fact]
    public async Task HandleAsync_FifthFailure_LocksAccount()
    {
        var user = SeedUser("OldPassword123", failedCount: ProfileRules.MaxFailedAccessAttempts - 1);

        var result = await CreateHandler().HandleAsync(Command(user.Id, user.TenantId, "NotThePassword1"));

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrorCodes.AccountLocked, result.Error!.Code);
        Assert.Equal(ProfileRules.MaxFailedAccessAttempts, user.AccessFailedCount);
        Assert.NotNull(user.LockoutEnd);
    }

    [Fact]
    public async Task HandleAsync_LockedAccount_RejectsEvenCorrectPassword()
    {
        var user = SeedUser(
            "OldPassword123",
            failedCount: ProfileRules.MaxFailedAccessAttempts,
            lockoutEnd: DateTimeOffset.UtcNow.AddMinutes(10));

        var result = await CreateHandler().HandleAsync(Command(user.Id, user.TenantId, "OldPassword123"));

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrorCodes.AccountLocked, result.Error!.Code);
    }

    [Fact]
    public async Task HandleAsync_WeakNewPassword_ReturnsPolicyViolation()
    {
        var user = SeedUser("OldPassword123");

        var result = await CreateHandler().HandleAsync(
            new ChangePasswordCommand(user.Id, user.TenantId, "OldPassword123", "short", "short"));

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrorCodes.PasswordPolicyViolation, result.Error!.Code);
        Assert.Equal(0, user.AccessFailedCount);
    }

    [Fact]
    public async Task HandleAsync_UnknownUser_ReturnsInvalidPasswordWithoutEnumeration()
    {
        var result = await CreateHandler().HandleAsync(
            Command(Guid.NewGuid(), Guid.NewGuid(), "Whatever123456"));

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrorCodes.InvalidPassword, result.Error!.Code);
    }
}
