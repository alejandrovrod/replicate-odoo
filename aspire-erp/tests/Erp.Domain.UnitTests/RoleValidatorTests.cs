using Erp.Domain.Entities.Security;
using Erp.Domain.Exceptions;
using Xunit;

namespace Erp.Domain.UnitTests;

/// <summary>
/// Role &amp; Permission Manager domain invariants (pure C#, no EF): name shape/charset,
/// tenant-scoped uniqueness as a pure exists-flag, System Manager immutability and the
/// permission-matrix semantics (catalog membership + Create ⇒ Write).
/// </summary>
public sealed class RoleValidatorTests
{
    private static Role NewRole(string name = "Support L1", bool isSystemDefault = false) =>
        new() { Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), Name = name, Description = "d", IsSystemDefault = isSystemDefault };

    private static Role SystemManager() => NewRole("System Manager", isSystemDefault: true);

    private static DocTypePermission Row(string docType = "sales_order", bool read = true, bool write = true, bool create = false) =>
        new()
        {
            Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), RoleId = Guid.NewGuid(),
            DocType = docType, CanRead = read, CanWrite = write, CanCreate = create,
        };

    private static readonly IReadOnlyCollection<string> Catalog =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "sales_order", "customer" };

    // ---------------------------------------------------------------- name shape

    [Fact]
    public void EnsureValidName_TrimsAndReturnsNormalized()
    {
        Assert.Equal("Support L1", RoleValidator.EnsureValidName("  Support L1  "));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EnsureValidName_Missing_ThrowsRoleNameInvalid(string? name)
    {
        var ex = Assert.Throws<AuthValidationException>(() => RoleValidator.EnsureValidName(name));
        Assert.Equal(AuthErrorCodes.RoleNameInvalid, ex.Code);
    }

    [Theory]
    [InlineData("AB")]
    [InlineData("a")]
    public void EnsureValidName_TooShort_ThrowsRoleNameInvalid(string name)
    {
        var ex = Assert.Throws<AuthValidationException>(() => RoleValidator.EnsureValidName(name));
        Assert.Equal(AuthErrorCodes.RoleNameInvalid, ex.Code);
    }

    [Fact]
    public void EnsureValidName_TooLong_ThrowsRoleNameInvalid()
    {
        var ex = Assert.Throws<AuthValidationException>(
            () => RoleValidator.EnsureValidName(new string('a', RoleValidator.MaxNameLength + 1)));
        Assert.Equal(AuthErrorCodes.RoleNameInvalid, ex.Code);
    }

    [Theory]
    [InlineData("Support@L1")]
    [InlineData("Rol/1")]
    [InlineData("A.B")]
    public void EnsureValidName_IllegalChars_ThrowsRoleNameInvalid(string name)
    {
        var ex = Assert.Throws<AuthValidationException>(() => RoleValidator.EnsureValidName(name));
        Assert.Equal(AuthErrorCodes.RoleNameInvalid, ex.Code);
    }

    [Theory]
    [InlineData("Support L1")]
    [InlineData("Sales_User-2")]
    [InlineData("ABC")]
    public void EnsureValidName_LegalNames_Pass(string name)
    {
        Assert.Equal(name, RoleValidator.EnsureValidName(name));
    }

    // ---------------------------------------------------------------- uniqueness (pure exists flag)

    [Fact]
    public void EnsureNameAvailable_Exists_ThrowsRoleNameConflict()
    {
        var ex = Assert.Throws<AuthValidationException>(
            () => RoleValidator.EnsureNameAvailable("Support L1", existsInTenant: true));
        Assert.Equal(AuthErrorCodes.RoleNameConflict, ex.Code);
    }

    [Fact]
    public void EnsureNameAvailable_Missing_Passes()
    {
        RoleValidator.EnsureNameAvailable("Support L1", existsInTenant: false);
    }

    // ---------------------------------------------------------------- System Manager immutability

    [Fact]
    public void EnsureRoleMutable_Null_ThrowsRoleNotFound()
    {
        var ex = Assert.Throws<AuthValidationException>(() => RoleValidator.EnsureRoleMutable(null));
        Assert.Equal(AuthErrorCodes.RoleNotFound, ex.Code);
    }

    [Fact]
    public void EnsureRoleMutable_SystemManager_ThrowsImmutable()
    {
        var ex = Assert.Throws<AuthValidationException>(() => RoleValidator.EnsureRoleMutable(SystemManager()));
        Assert.Equal(AuthErrorCodes.SystemManagerImmutable, ex.Code);
    }

    [Fact]
    public void EnsureRoleMutable_SeededFunctionalRole_Passes()
    {
        // Seeded functional roles ARE IsSystemDefault - only the System Manager name is guarded.
        RoleValidator.EnsureRoleMutable(NewRole("Sales User", isSystemDefault: true));
    }

    [Fact]
    public void EnsureRoleMutable_CustomRole_Passes()
    {
        RoleValidator.EnsureRoleMutable(NewRole());
    }

    [Fact]
    public void IsSystemManager_RequiresBothFlagAndName()
    {
        Assert.True(RoleValidator.IsSystemManager(SystemManager()));
        Assert.False(RoleValidator.IsSystemManager(NewRole("System Manager", isSystemDefault: false)));
        Assert.False(RoleValidator.IsSystemManager(NewRole("Sales User", isSystemDefault: true)));
    }

    // ---------------------------------------------------------------- matrix semantics

    [Fact]
    public void EnsurePermissionsMatrix_ValidMatrix_Passes()
    {
        RoleValidator.EnsurePermissionsMatrix(
            NewRole(), new[] { Row("sales_order"), Row("customer", read: true, write: false) }, Catalog);
    }

    [Fact]
    public void EnsurePermissionsMatrix_CreateWithoutWrite_ThrowsExplicitFailure()
    {
        var ex = Assert.Throws<AuthValidationException>(() =>
            RoleValidator.EnsurePermissionsMatrix(
                NewRole(), new[] { Row("sales_order", write: false, create: true) }, Catalog));
        Assert.Equal(AuthErrorCodes.RolePermissionInvalid, ex.Code);
        Assert.Contains("Create", ex.Message);
        Assert.Contains("Write", ex.Message);
    }

    [Fact]
    public void EnsurePermissionsMatrix_UnknownDocType_ThrowsPermissionInvalid()
    {
        var ex = Assert.Throws<AuthValidationException>(() =>
            RoleValidator.EnsurePermissionsMatrix(NewRole(), new[] { Row("purchase_order") }, Catalog));
        Assert.Equal(AuthErrorCodes.RolePermissionInvalid, ex.Code);
    }

    [Fact]
    public void EnsurePermissionsMatrix_SystemManagerWithRows_ThrowsImmutable()
    {
        var ex = Assert.Throws<AuthValidationException>(() =>
            RoleValidator.EnsurePermissionsMatrix(SystemManager(), Array.Empty<DocTypePermission>(), Catalog));
        Assert.Equal(AuthErrorCodes.SystemManagerImmutable, ex.Code);
    }
}
