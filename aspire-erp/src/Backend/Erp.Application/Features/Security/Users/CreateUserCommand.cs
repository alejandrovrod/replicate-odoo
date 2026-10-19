using Erp.Application.Common;

namespace Erp.Application.Features.Security.Users;

/// <summary>
/// Creates a tenant user with role assignments (spec D2 USER-G2). Email uniqueness is
/// tenant-scoped: the same address in another tenant is legal.
/// </summary>
public sealed record CreateUserCommand(
    string FullName,
    string Email,
    string? TemporaryPassword,
    IReadOnlyList<Guid> RoleIds,
    bool IsActive = true) : ICommand<Result<CreateUserResult>>;
