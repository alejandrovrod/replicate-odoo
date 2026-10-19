using Erp.Domain.Exceptions;

namespace Erp.Domain.Entities.Security;

/// <summary>
/// Pure C# invariants for the User Management aggregate (user-management spec D1/D3).
/// No EF Core, no NuGet packages - Constitution Article I.2 keeps Erp.Domain dependency-free,
/// so every rule here is unit-tested without a database (mirrors <see cref="RoleValidator"/>).
/// </summary>
/// <remarks>
/// Tenant-scoped uniqueness and the last-admin count are pure functions of caller-resolved
/// flags: the handlers query <c>IUserAdminRepository</c> and pass the booleans in, keeping the
/// rules deterministic and the database access out of Domain.
/// </remarks>
public static class UserValidator
{
    /// <summary><c>Users.Email</c> is <c>NVARCHAR(256)</c> (spec D3).</summary>
    public const int MaxEmailLength = 256;

    /// <summary><c>Users.FullName</c> is <c>NVARCHAR(200)</c> (spec D3).</summary>
    public const int MaxFullNameLength = 200;

    /// <summary>Trims and lowercases the email so the tenant unique index is deterministic.</summary>
    public static string NormalizeEmail(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>
    /// Field shape rules: required full name (max 200) and a syntactically valid email
    /// (max 256). Anything else is <c>INVALID_USER_DATA</c> (400).
    /// </summary>
    /// <exception cref="AuthValidationException"><c>INVALID_USER_DATA</c>.</exception>
    public static void EnsureValidFields(string? fullName, string? email)
    {
        var normalizedName = fullName?.Trim() ?? string.Empty;

        if (normalizedName.Length == 0)
        {
            throw new AuthValidationException(
                AuthErrorCodes.InvalidUserData,
                "Full name is required.");
        }

        if (normalizedName.Length > MaxFullNameLength)
        {
            throw new AuthValidationException(
                AuthErrorCodes.InvalidUserData,
                $"Full name must not exceed {MaxFullNameLength} characters.");
        }

        var normalizedEmail = NormalizeEmail(email);

        if (normalizedEmail.Length == 0)
        {
            throw new AuthValidationException(
                AuthErrorCodes.InvalidUserData,
                "Email is required.");
        }

        if (normalizedEmail.Length > MaxEmailLength)
        {
            throw new AuthValidationException(
                AuthErrorCodes.InvalidUserData,
                $"Email must not exceed {MaxEmailLength} characters.");
        }

        if (!IsValidEmail(normalizedEmail))
        {
            throw new AuthValidationException(
                AuthErrorCodes.InvalidUserData,
                "Email is not a valid address.");
        }
    }

    /// <summary>
    /// Tenant-scoped email uniqueness (spec D2 USER-G2): the handler resolves
    /// <paramref name="existsInTenant"/> against the tenant-filtered repository, so the same
    /// address in another tenant remains legal.
    /// </summary>
    /// <exception cref="AuthValidationException"><c>USER_EMAIL_CONFLICT</c>.</exception>
    public static void EnsureEmailAvailable(bool existsInTenant, string? email)
    {
        if (existsInTenant)
        {
            throw new AuthValidationException(
                AuthErrorCodes.UserEmailConflict,
                $"A user with email '{NormalizeEmail(email)}' already exists in this tenant.");
        }
    }

    /// <summary>
    /// Optimistic concurrency compare-and-swap (spec D2 USER-G3): a missing or different token
    /// is <c>STALE_ROW_VERSION</c> (409), never a silent overwrite.
    /// </summary>
    /// <exception cref="AuthValidationException"><c>STALE_ROW_VERSION</c>.</exception>
    public static void EnsureRowVersion(byte[]? current, byte[]? requested)
    {
        if (requested is null
            || current is null
            || !current.AsSpan().SequenceEqual(requested))
        {
            throw new AuthValidationException(
                AuthErrorCodes.StaleRowVersion,
                "The user was modified by another operation. Reload it and try again.");
        }
    }

    /// <summary>
    /// Last-active-administrator invariant (spec D1/D2 USER-G4): deactivating or removing the
    /// System Manager role from the only active holder is refused with
    /// <c>CANNOT_REMOVE_LAST_ADMIN</c> (409).
    /// </summary>
    /// <exception cref="AuthValidationException"><c>CANNOT_REMOVE_LAST_ADMIN</c>.</exception>
    public static void EnsureAnotherActiveAdminRemains(
        bool isActiveSystemManager,
        bool hasAnotherActiveSystemManager)
    {
        if (isActiveSystemManager && !hasAnotherActiveSystemManager)
        {
            throw new AuthValidationException(
                AuthErrorCodes.CannotRemoveLastAdmin,
                "The tenant must keep at least one active System Manager.");
        }
    }

    /// <summary>
    /// RFC-lite address shape: non-empty local part, one <c>@</c> and a dotted domain.
    /// Kept intentionally simple and in lockstep with the frontend form rule.
    /// </summary>
    public static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        var at = email.IndexOf('@');
        if (at <= 0 || at != email.LastIndexOf('@') || at == email.Length - 1)
        {
            return false;
        }

        var domain = email[(at + 1)..];
        return !domain.StartsWith('.')
            && !domain.EndsWith('.')
            && domain.Contains('.')
            && !domain.Contains(' ');
    }
}
