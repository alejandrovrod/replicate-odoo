using Erp.Domain.Exceptions;

namespace Erp.Domain.Entities.Security;

/// <summary>
/// Pure C# validation for the Role &amp; Permission Manager aggregates
/// (.specify/modules/role-permission-manager/spec.md §2/§4). No EF Core, no NuGet
/// packages - Constitution Article I keeps Erp.Domain dependency-free, so every rule
/// here is unit-tested without a database (mirrors <c>ProfileValidator</c>).
/// </summary>
/// <remarks>
/// Purity contract (tasks.md): the tenant-scoped duplicate check is a pure function of
/// an <c>exists</c> flag - the handler resolves existence against
/// <c>IRoleRepository</c>; the permission-matrix check receives the official DocType
/// catalog as a plain collection (the catalog itself lives in Erp.Application, which
/// Domain must not reference). Invariants throw <see cref="AuthValidationException"/>
/// carrying the spec's UPPER_SNAKE codes so handlers map them to RFC 7807 responses.
/// </remarks>
public static class RoleValidator
{
    /// <summary>Canonical root-role name (spec §0: the authorization bypass role).</summary>
    public const string SystemManagerRoleName = "System Manager";

    /// <summary>Role names are 3..100 chars (spec §4 DDL: <c>Name NVARCHAR(100)</c>).</summary>
    public const int MinNameLength = 3;
    public const int MaxNameLength = 100;

    /// <summary>
    /// Derived root-role predicate (plan.md Phase 1.2): the flag is
    /// <c>IsSystemDefault &amp;&amp; Name == "System Manager"</c> - there is NO
    /// <c>IsSystemRole</c> column (spec §0/D6). Functional seeded roles are
    /// <c>IsSystemDefault</c> too, but only System Manager is immutable.
    /// </summary>
    public static bool IsSystemManager(Role role) =>
        role is not null
        && role.IsSystemDefault
        && string.Equals(role.Name, SystemManagerRoleName, StringComparison.Ordinal);

    /// <summary>Trims the candidate name (spec: normalization is trim).</summary>
    public static string NormalizeName(string? name) => name?.Trim() ?? string.Empty;

    /// <summary>
    /// Name shape rules: required, <see cref="MinNameLength"/>..<see cref="MaxNameLength"/>
    /// chars after trim, charset <c>[A-Za-z0-9 _-]</c>.
    /// </summary>
    /// <returns>The trimmed name, ready to persist.</returns>
    /// <exception cref="AuthValidationException"><c>ROLE_NAME_INVALID</c>.</exception>
    public static string EnsureValidName(string? name)
    {
        var normalized = NormalizeName(name);

        if (normalized.Length == 0)
        {
            throw new AuthValidationException(
                AuthErrorCodes.RoleNameInvalid,
                "Role name is required.");
        }

        if (normalized.Length < MinNameLength || normalized.Length > MaxNameLength)
        {
            throw new AuthValidationException(
                AuthErrorCodes.RoleNameInvalid,
                $"Role name must be between {MinNameLength} and {MaxNameLength} characters.");
        }

        foreach (var ch in normalized)
        {
            var ok = char.IsAsciiLetterOrDigit(ch) || ch == ' ' || ch == '_' || ch == '-';
            if (!ok)
            {
                throw new AuthValidationException(
                    AuthErrorCodes.RoleNameInvalid,
                    "Role name may only contain letters, digits, spaces, underscores and hyphens.");
            }
        }

        return normalized;
    }

    /// <summary>
    /// Tenant-scoped uniqueness as a pure function of an existence flag: the handler
    /// resolves <paramref name="existsInTenant"/> against the repository (UQ
    /// <c>Roles(TenantId, Name)</c>), so cross-tenant duplicates stay legal (SEC-G2).
    /// </summary>
    /// <exception cref="AuthValidationException"><c>ROLE_NAME_CONFLICT</c>.</exception>
    public static void EnsureNameAvailable(string normalizedName, bool existsInTenant)
    {
        if (existsInTenant)
        {
            throw new AuthValidationException(
                AuthErrorCodes.RoleNameConflict,
                $"A role named '{normalizedName}' already exists in this tenant.");
        }
    }

    /// <summary>
    /// Mutability guard (SEC-G1/SEC-G5): System Manager owns no permission rows by design
    /// (authorization bypass, seeder §0) - any <c>UpdatePermissions</c> over it is
    /// <c>SYSTEM_MANAGER_IMMUTABLE</c>. Functional roles (even seeded ones) stay editable.
    /// </summary>
    /// <exception cref="AuthValidationException">
    /// <c>ROLE_NOT_FOUND</c> (null) or <c>SYSTEM_MANAGER_IMMUTABLE</c>.
    /// </exception>
    public static void EnsureRoleMutable(Role? role)
    {
        if (role is null)
        {
            throw new AuthValidationException(
                AuthErrorCodes.RoleNotFound,
                "Role not found.");
        }

        if (IsSystemManager(role))
        {
            throw new AuthValidationException(
                AuthErrorCodes.SystemManagerImmutable,
                "The System Manager role is immutable: it owns no permission rows (authorization bypass).");
        }
    }

    /// <summary>
    /// Permission-matrix semantics (spec §4/§5): the role must be mutable, every row's
    /// <c>DocType</c> must belong to the official catalog, and <c>Create ⇒ Write</c>
    /// (a row with <c>Create</c> but no <c>Write</c> is rejected explicitly - never
    /// auto-coerced, so the semantics stay visible).
    /// </summary>
    /// <exception cref="AuthValidationException">
    /// <c>SYSTEM_MANAGER_IMMUTABLE</c> or <c>ROLE_PERMISSION_INVALID</c>.
    /// </exception>
    public static void EnsurePermissionsMatrix(
        Role? role,
        IReadOnlyList<DocTypePermission> rows,
        IReadOnlyCollection<string> allowedDocTypes)
    {
        EnsureRoleMutable(role);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(allowedDocTypes);

        foreach (var row in rows)
        {
            if (row is null || !allowedDocTypes.Contains(row.DocType))
            {
                throw new AuthValidationException(
                    AuthErrorCodes.RolePermissionInvalid,
                    $"Unknown DocType '{row?.DocType}': it is not part of the official catalog.");
            }

            if (row.CanCreate && !row.CanWrite)
            {
                throw new AuthValidationException(
                    AuthErrorCodes.RolePermissionInvalid,
                    $"Invalid row for DocType '{row.DocType}': Create requires Write (Create ⇒ Write).");
            }
        }
    }
}
