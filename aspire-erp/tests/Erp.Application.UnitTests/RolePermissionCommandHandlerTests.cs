using Erp.Application.Features.Security.Roles;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities.Security;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Role &amp; Permission Manager command coverage (SEC-G1/SEC-G2/SEC-G3/SEC-G5):
/// matrix updates reject System Manager (403) and persist valid matrices; assignments
/// guard the last admin (409) and cross-tenant writes (403); creation enforces the
/// tenant-scoped unique name (409).
/// </summary>
public sealed class RolePermissionCommandHandlerTests
{
    private static readonly Guid Tenant = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid OtherTenant = Guid.Parse("22222222-2222-4222-8222-222222222222");

    private static FakeRoleRepository NewRepo() => new();

    private static Role SystemManager(Guid? tenantId = null) =>
        FakeRoleRepository.NewRole(name: "System Manager", isSystemDefault: true, tenantId: tenantId);

    private static Role FunctionalRole(string name = "Sales User", Guid? tenantId = null, bool seeded = true) =>
        FakeRoleRepository.NewRole(name: name, isSystemDefault: seeded, tenantId: tenantId);

    private static UserRole Link(Guid userId, Guid roleId, Guid? tenantId = null) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId ?? Tenant,
        UserId = userId,
        RoleId = roleId,
    };

    private static DocTypePermissionDto Perm(
        string docType = "sales_order",
        bool read = true,
        bool write = true,
        bool create = false) =>
        new(docType, read, write, create, CanDelete: false, CanSubmit: false, CanCancel: false);

    // ---------------------------------------------------------------- create (SEC-G2)

    [Fact]
    public async Task CreateRole_ValidName_CreatesFunctionalRole()
    {
        var repo = NewRepo();

        var result = await new CreateRoleCommandHandler(repo)
            .HandleAsync(new CreateRoleCommand("Support L1", "Help desk", Tenant));

        Assert.True(result.IsSuccess);
        Assert.Equal("Support L1", result.Value!.Name);
        Assert.False(result.Value.IsSystemDefault);
        Assert.Equal(0, result.Value.UserCount);
    }

    [Fact]
    public async Task CreateRole_DuplicateNameInTenant_ReturnsNameConflict()
    {
        var repo = NewRepo();
        repo.SeedRoles(FunctionalRole("Support L1"));

        var result = await new CreateRoleCommandHandler(repo)
            .HandleAsync(new CreateRoleCommand("  Support L1 ", "dup", Tenant));

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrorCodes.RoleNameConflict, result.Error!.Code);
    }

    [Fact]
    public async Task CreateRole_InvalidName_ReturnsNameInvalid()
    {
        var repo = NewRepo();

        var result = await new CreateRoleCommandHandler(repo)
            .HandleAsync(new CreateRoleCommand("AB", "too short", Tenant));

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrorCodes.RoleNameInvalid, result.Error!.Code);
    }

    // ---------------------------------------------------------------- update matrix (SEC-G1/SEC-G5)

    [Fact]
    public async Task UpdatePermissions_SystemManager_ReturnsImmutable()
    {
        var repo = NewRepo();
        var sm = SystemManager();
        repo.SeedRoles(sm);

        var result = await new UpdateRolePermissionsCommandHandler(repo)
            .HandleAsync(new UpdateRolePermissionsCommand(sm.Id, Tenant, new[] { Perm() }));

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrorCodes.SystemManagerImmutable, result.Error!.Code);
    }

    [Fact]
    public async Task UpdatePermissions_FunctionalRole_PersistsMatrix()
    {
        var repo = NewRepo();
        var role = FunctionalRole();
        repo.SeedRoles(role);

        var result = await new UpdateRolePermissionsCommandHandler(repo)
            .HandleAsync(new UpdateRolePermissionsCommand(
                role.Id, Tenant, new[] { Perm("sales_order"), Perm("customer") }));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Permissions.Count);

        var reread = await repo.GetRoleByIdAsync(role.Id);
        Assert.Equal(2, reread!.Permissions.Count);
    }

    [Fact]
    public async Task UpdatePermissions_CreateWithoutWrite_ReturnsPermissionInvalid()
    {
        var repo = NewRepo();
        var role = FunctionalRole();
        repo.SeedRoles(role);

        var result = await new UpdateRolePermissionsCommandHandler(repo)
            .HandleAsync(new UpdateRolePermissionsCommand(
                role.Id, Tenant, new[] { Perm("sales_order", write: false, create: true) }));

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrorCodes.RolePermissionInvalid, result.Error!.Code);
    }

    [Fact]
    public async Task UpdatePermissions_UnknownDocType_ReturnsPermissionInvalid()
    {
        var repo = NewRepo();
        var role = FunctionalRole();
        repo.SeedRoles(role);

        var result = await new UpdateRolePermissionsCommandHandler(repo)
            .HandleAsync(new UpdateRolePermissionsCommand(
                role.Id, Tenant, new[] { Perm("invented_doctype") }));

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrorCodes.RolePermissionInvalid, result.Error!.Code);
    }

    [Fact]
    public async Task UpdatePermissions_UnknownRole_ReturnsNotFound()
    {
        var repo = NewRepo();

        var result = await new UpdateRolePermissionsCommandHandler(repo)
            .HandleAsync(new UpdateRolePermissionsCommand(Guid.NewGuid(), Tenant, Array.Empty<DocTypePermissionDto>()));

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrorCodes.RoleNotFound, result.Error!.Code);
    }

    [Fact]
    public async Task UpdatePermissions_CrossTenantRole_ReturnsCrossTenantDenied()
    {
        var repo = NewRepo();
        var role = FunctionalRole(tenantId: OtherTenant);
        repo.SeedRoles(role);

        var result = await new UpdateRolePermissionsCommandHandler(repo)
            .HandleAsync(new UpdateRolePermissionsCommand(role.Id, Tenant, Array.Empty<DocTypePermissionDto>()));

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrorCodes.CrossTenantAssignmentDenied, result.Error!.Code);
    }

    // ---------------------------------------------------------------- assign (SEC-G3/SEC-G5)

    [Fact]
    public async Task AssignRoles_RemovingLastSystemManager_ReturnsCannotRemoveLastAdmin()
    {
        var repo = NewRepo();
        var sm = SystemManager();
        var sales = FunctionalRole();
        var admin = FakeRoleRepository.NewUser();
        repo.SeedRoles(sm, sales);
        repo.SeedUsers(admin);
        repo.SeedUserRoles(Link(admin.Id, sm.Id));

        var result = await new AssignUserRolesCommandHandler(repo)
            .HandleAsync(new AssignUserRolesCommand(admin.Id, Tenant, new[] { sales.Id }));

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrorCodes.CannotRemoveLastAdmin, result.Error!.Code);
    }

    [Fact]
    public async Task AssignRoles_RemovingSystemManagerWithOtherAdmin_Succeeds()
    {
        var repo = NewRepo();
        var sm = SystemManager();
        var sales = FunctionalRole();
        var admin = FakeRoleRepository.NewUser(email: "a@example.com");
        var other = FakeRoleRepository.NewUser(email: "b@example.com");
        repo.SeedRoles(sm, sales);
        repo.SeedUsers(admin, other);
        repo.SeedUserRoles(Link(admin.Id, sm.Id), Link(other.Id, sm.Id));

        var result = await new AssignUserRolesCommandHandler(repo)
            .HandleAsync(new AssignUserRolesCommand(admin.Id, Tenant, new[] { sales.Id }));

        Assert.True(result.IsSuccess);
        var reread = await repo.GetUserWithRolesUnfilteredAsync(admin.Id);
        Assert.Single(reread!.UserRoles);
        Assert.Equal(sales.Id, reread.UserRoles.Single().RoleId);
    }

    [Fact]
    public async Task AssignRoles_CrossTenantUser_ReturnsCrossTenantDenied()
    {
        var repo = NewRepo();
        var sm = SystemManager();
        var foreign = FakeRoleRepository.NewUser(tenantId: OtherTenant);
        repo.SeedRoles(sm);
        repo.SeedUsers(foreign);

        var result = await new AssignUserRolesCommandHandler(repo)
            .HandleAsync(new AssignUserRolesCommand(foreign.Id, Tenant, new[] { sm.Id }));

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrorCodes.CrossTenantAssignmentDenied, result.Error!.Code);
    }

    [Fact]
    public async Task AssignRoles_CrossTenantRole_ReturnsCrossTenantDenied()
    {
        var repo = NewRepo();
        var foreignRole = FunctionalRole(tenantId: OtherTenant);
        var user = FakeRoleRepository.NewUser();
        repo.SeedRoles(foreignRole);
        repo.SeedUsers(user);

        var result = await new AssignUserRolesCommandHandler(repo)
            .HandleAsync(new AssignUserRolesCommand(user.Id, Tenant, new[] { foreignRole.Id }));

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrorCodes.CrossTenantAssignmentDenied, result.Error!.Code);
    }

    [Fact]
    public async Task AssignRoles_UnknownUser_ReturnsUserRoleNotFound()
    {
        var repo = NewRepo();

        var result = await new AssignUserRolesCommandHandler(repo)
            .HandleAsync(new AssignUserRolesCommand(Guid.NewGuid(), Tenant, Array.Empty<Guid>()));

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrorCodes.UserRoleNotFound, result.Error!.Code);
    }

    [Fact]
    public async Task AssignRoles_UnknownRole_ReturnsRoleNotFound()
    {
        var repo = NewRepo();
        var user = FakeRoleRepository.NewUser();
        repo.SeedUsers(user);

        var result = await new AssignUserRolesCommandHandler(repo)
            .HandleAsync(new AssignUserRolesCommand(user.Id, Tenant, new[] { Guid.NewGuid() }));

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrorCodes.RoleNotFound, result.Error!.Code);
    }

    [Fact]
    public async Task AssignRoles_ValidSet_ReplacesRoles()
    {
        var repo = NewRepo();
        var sm = SystemManager();
        var sales = FunctionalRole();
        var user = FakeRoleRepository.NewUser();
        repo.SeedRoles(sm, sales);
        repo.SeedUsers(user);
        repo.SeedUserRoles(Link(user.Id, sm.Id));

        var result = await new AssignUserRolesCommandHandler(repo)
            .HandleAsync(new AssignUserRolesCommand(user.Id, Tenant, new[] { sm.Id, sales.Id }));

        Assert.True(result.IsSuccess);
        var reread = await repo.GetUserWithRolesUnfilteredAsync(user.Id);
        Assert.Equal(2, reread!.UserRoles.Count);
    }
}
