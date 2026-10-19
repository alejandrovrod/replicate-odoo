using Erp.Application.Features.Security.Users;
using Erp.Application.Services;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities.Security;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// User Management (user-management spec D2/D4): tenant-scoped lifecycle, last-active-admin
/// guard and optimistic concurrency, exercised through the in-house CQRS handlers with the
/// tenant-filtering <see cref="FakeUserAdminRepository"/>.
/// </summary>
public sealed class UserManagementCommandHandlerTests
{
    private readonly FakeUserAdminRepository _users = new();
    private readonly PasswordHasher _passwords = new();
    private readonly Role _systemManager = FakeUserAdminRepository.SystemManager();
    private readonly Role _accountant = FakeUserAdminRepository.FunctionalRole("Accountant");

    private CreateUserCommandHandler CreateHandler() => new(_users, _passwords);

    private UpdateUserCommandHandler UpdateHandler() => new(_users);

    private ToggleUserStatusCommandHandler ToggleHandler() => new(_users);

    private ResetUserPasswordCommandHandler ResetHandler() => new(_users, _passwords);

    public UserManagementCommandHandlerTests()
    {
        _users.SeedRole(_systemManager);
        _users.SeedRole(_accountant);
    }

    // ------------------------------------------------------------------ create

    [Fact]
    public async Task Create_NewUser_NormalizesEmailAndHashesPassword()
    {
        const string plaintext = "Temporal123!";

        var result = await CreateHandler().HandleAsync(new CreateUserCommand(
            "  Ada Lovelace ",
            "  ADA@Example.COM ",
            plaintext,
            new[] { _accountant.Id }));

        Assert.True(result.IsSuccess);
        var dto = result.Value!.User;
        Assert.Equal(plaintext, result.Value.TemporaryPassword);
        Assert.Equal("ada@example.com", dto.Email);
        Assert.Equal("Ada Lovelace", dto.FullName);
        Assert.True(dto.IsActive);
        Assert.Equal(new[] { _accountant.Id }, dto.RoleIds);
        Assert.Equal(new[] { "Accountant" }, dto.RoleNames);

        var persisted = Assert.Single(_users.Users);
        Assert.Equal(_users.TenantId, persisted.TenantId);
        Assert.NotEqual(plaintext, persisted.PasswordHash);
        Assert.True(_passwords.Verify(plaintext, persisted.PasswordHash));
    }

    [Fact]
    public async Task Create_DuplicateEmailInTenant_ReturnsUserEmailConflict()
    {
        _users.SeedUser(FakeUserAdminRepository.NewUser("taken@example.com", roles: _accountant));

        var result = await CreateHandler().HandleAsync(new CreateUserCommand(
            "Another User",
            "TAKEN@example.com",
            "Temporal123!",
            new[] { _accountant.Id }));

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrorCodes.UserEmailConflict, result.Error!.Code);
    }

    [Fact]
    public async Task Create_SameEmailInAnotherTenant_Succeeds()
    {
        var otherTenant = Guid.NewGuid();
        _users.SeedUser(FakeUserAdminRepository.NewUser(
            "shared@example.com",
            tenantId: otherTenant));

        var result = await CreateHandler().HandleAsync(new CreateUserCommand(
            "Tenant User",
            "shared@example.com",
            "Temporal123!",
            new[] { _accountant.Id }));

        Assert.True(result.IsSuccess);
        Assert.Equal("shared@example.com", result.Value!.User.Email);
    }

    [Fact]
    public async Task Create_WithoutTemporaryPassword_GeneratesOne()
    {
        var result = await CreateHandler().HandleAsync(new CreateUserCommand(
            "Generated Password",
            "generated@example.com",
            TemporaryPassword: null,
            new[] { _accountant.Id }));

        Assert.True(result.IsSuccess);
        var generated = result.Value!.TemporaryPassword;
        Assert.False(string.IsNullOrWhiteSpace(generated));
        Assert.True(generated.Length >= 8);
        Assert.True(_passwords.Verify(generated, Assert.Single(_users.Users).PasswordHash));
    }

    [Fact]
    public async Task Create_WithoutRoles_ReturnsInvalidUserData()
    {
        var result = await CreateHandler().HandleAsync(new CreateUserCommand(
            "No Role",
            "norole@example.com",
            "Temporal123!",
            Array.Empty<Guid>()));

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrorCodes.InvalidUserData, result.Error!.Code);
    }

    [Fact]
    public async Task Create_WithUnknownRole_ReturnsInvalidUserData()
    {
        var result = await CreateHandler().HandleAsync(new CreateUserCommand(
            "Ghost Role",
            "ghost@example.com",
            "Temporal123!",
            new[] { Guid.NewGuid() }));

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrorCodes.InvalidUserData, result.Error!.Code);
    }

    // ------------------------------------------------------------------ update

    [Fact]
    public async Task Update_StaleRowVersion_ReturnsStaleRowVersion()
    {
        var user = FakeUserAdminRepository.NewUser("ada@example.com", roles: _accountant);
        user.RowVersion = new byte[] { 1, 2, 3 };
        _users.SeedUser(user);

        var result = await UpdateHandler().HandleAsync(new UpdateUserCommand(
            user.Id,
            "Ada Lovelace",
            new[] { _accountant.Id },
            IsActive: true,
            RowVersion: new byte[] { 9, 9, 9 }));

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrorCodes.StaleRowVersion, result.Error!.Code);
    }

    [Fact]
    public async Task Update_NullIsActive_PreservesCurrentStatus()
    {
        var user = FakeUserAdminRepository.NewUser("ada@example.com", roles: _accountant);
        user.RowVersion = new byte[] { 1 };
        _users.SeedUser(user);

        var result = await UpdateHandler().HandleAsync(new UpdateUserCommand(
            user.Id,
            "Ada Byron",
            new[] { _accountant.Id },
            IsActive: null,
            RowVersion: new byte[] { 1 }));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsActive);
        Assert.Equal("Ada Byron", user.FullName);
    }

    [Fact]
    public async Task Update_DeactivatingLastActiveSystemManager_ReturnsCannotRemoveLastAdmin()
    {
        var admin = FakeUserAdminRepository.NewUser("admin@example.com", roles: _systemManager);
        admin.RowVersion = new byte[] { 1 };
        _users.SeedUser(admin);

        var result = await UpdateHandler().HandleAsync(new UpdateUserCommand(
            admin.Id,
            admin.FullName,
            new[] { _systemManager.Id },
            IsActive: false,
            RowVersion: new byte[] { 1 }));

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrorCodes.CannotRemoveLastAdmin, result.Error!.Code);
        Assert.True(admin.IsActive);
    }

    [Fact]
    public async Task Update_RemovingSystemManagerRoleFromLastAdmin_ReturnsCannotRemoveLastAdmin()
    {
        var admin = FakeUserAdminRepository.NewUser("admin@example.com", roles: _systemManager);
        admin.RowVersion = new byte[] { 1 };
        _users.SeedUser(admin);

        var result = await UpdateHandler().HandleAsync(new UpdateUserCommand(
            admin.Id,
            admin.FullName,
            new[] { _accountant.Id },
            IsActive: true,
            RowVersion: new byte[] { 1 }));

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrorCodes.CannotRemoveLastAdmin, result.Error!.Code);
    }

    [Fact]
    public async Task Update_DeactivatingAdminWhenAnotherActiveAdminRemains_Succeeds()
    {
        var admin = FakeUserAdminRepository.NewUser("admin@example.com", roles: _systemManager);
        admin.RowVersion = new byte[] { 1 };
        _users.SeedUser(admin);
        _users.SeedUser(FakeUserAdminRepository.NewUser("admin2@example.com", roles: _systemManager));

        var result = await UpdateHandler().HandleAsync(new UpdateUserCommand(
            admin.Id,
            admin.FullName,
            new[] { _systemManager.Id },
            IsActive: false,
            RowVersion: new byte[] { 1 }));

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.IsActive);
    }

    // ------------------------------------------------------------------ toggle

    [Fact]
    public async Task Toggle_DeactivatingLastActiveSystemManager_ReturnsCannotRemoveLastAdmin()
    {
        var admin = FakeUserAdminRepository.NewUser("admin@example.com", roles: _systemManager);
        _users.SeedUser(admin);

        var result = await ToggleHandler().HandleAsync(
            new ToggleUserStatusCommand(admin.Id, IsActive: false));

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrorCodes.CannotRemoveLastAdmin, result.Error!.Code);
        Assert.True(admin.IsActive);
    }

    [Fact]
    public async Task Toggle_Idempotent_WhenAlreadyInRequestedState()
    {
        var user = FakeUserAdminRepository.NewUser(
            "ada@example.com",
            isActive: false,
            roles: _accountant);
        _users.SeedUser(user);

        var result = await ToggleHandler().HandleAsync(
            new ToggleUserStatusCommand(user.Id, IsActive: false));

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.IsActive);
    }

    [Fact]
    public async Task Toggle_ActivatingInactiveUser_Succeeds()
    {
        var user = FakeUserAdminRepository.NewUser(
            "ada@example.com",
            isActive: false,
            roles: _accountant);
        _users.SeedUser(user);

        var result = await ToggleHandler().HandleAsync(
            new ToggleUserStatusCommand(user.Id, IsActive: true));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsActive);
    }

    // ------------------------------------------------------------------ reset password

    [Fact]
    public async Task ResetPassword_AssignsSuppliedPasswordAndClearsLockout()
    {
        const string temporary = "Temporal123!";
        var user = FakeUserAdminRepository.NewUser("ada@example.com", roles: _accountant);
        user.AccessFailedCount = 5;
        user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(10);
        _users.SeedUser(user);

        var result = await ResetHandler().HandleAsync(
            new ResetUserPasswordCommand(user.Id, temporary));

        Assert.True(result.IsSuccess);
        Assert.Equal(temporary, result.Value!.TemporaryPassword);
        Assert.True(_passwords.Verify(temporary, user.PasswordHash));
        Assert.Equal(0, user.AccessFailedCount);
        Assert.Null(user.LockoutEnd);
    }

    [Fact]
    public async Task ResetPassword_WithoutSuppliedPassword_GeneratesOne()
    {
        var user = FakeUserAdminRepository.NewUser("ada@example.com", roles: _accountant);
        _users.SeedUser(user);

        var result = await ResetHandler().HandleAsync(new ResetUserPasswordCommand(user.Id));

        Assert.True(result.IsSuccess);
        var generated = result.Value!.TemporaryPassword;
        Assert.False(string.IsNullOrWhiteSpace(generated));
        Assert.True(generated.Length >= 8);
        Assert.True(_passwords.Verify(generated, user.PasswordHash));
    }

    [Fact]
    public async Task ResetPassword_UnknownUser_ReturnsUserNotFound()
    {
        var result = await ResetHandler().HandleAsync(
            new ResetUserPasswordCommand(Guid.NewGuid(), "Temporal123!"));

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrorCodes.UserNotFound, result.Error!.Code);
    }
}
