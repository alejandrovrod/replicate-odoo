using Erp.Application.Common;
using Erp.Domain.Entities.Security;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Security.Users;

/// <summary>Single-user read with role assignments (edit dialog / detail).</summary>
public sealed record GetUserByIdQuery(Guid UserId) : IQuery<Result<UserAdminDto>>;

public sealed class GetUserByIdQueryHandler
    : IQueryHandler<GetUserByIdQuery, Result<UserAdminDto>>
{
    private readonly IUserAdminRepository _users;

    public GetUserByIdQueryHandler(IUserAdminRepository users)
    {
        _users = users;
    }

    public async Task<Result<UserAdminDto>> HandleAsync(
        GetUserByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdWithRolesAsync(query.UserId, cancellationToken);
        return user is null
            ? Result<UserAdminDto>.Failure(
                AuthErrorCodes.UserNotFound,
                $"User '{query.UserId}' was not found in this tenant.")
            : Result<UserAdminDto>.Success(UserAdminDto.From(user));
    }
}
