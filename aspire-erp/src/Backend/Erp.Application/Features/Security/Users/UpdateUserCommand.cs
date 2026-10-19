using Erp.Application.Common;

namespace Erp.Application.Features.Security.Users;

/// <summary>
/// Edits a user's display name, role set and lifecycle flag under optimistic concurrency
/// (spec D2 USER-G3). Email is immutable for administrators: identity changes require a
/// dedicated audited flow, so the command does not accept one.
/// </summary>
public sealed record UpdateUserCommand(
    Guid UserId,
    string FullName,
    IReadOnlyList<Guid> RoleIds,
    bool? IsActive,
    byte[]? RowVersion) : ICommand<Result<UserAdminDto>>;
