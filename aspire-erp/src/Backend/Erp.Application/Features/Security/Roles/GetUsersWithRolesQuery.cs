using Erp.Application.Common;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Security.Roles;

/// <summary>Every user of the current tenant with its role names (assignment dialog read).</summary>
public sealed record GetUsersWithRolesQuery : IQuery<IReadOnlyList<UserWithRolesDto>>;

public sealed class GetUsersWithRolesQueryHandler : IQueryHandler<GetUsersWithRolesQuery, IReadOnlyList<UserWithRolesDto>>
{
    private readonly IRoleRepository _roles;

    public GetUsersWithRolesQueryHandler(IRoleRepository roles)
    {
        _roles = roles;
    }

    public async Task<IReadOnlyList<UserWithRolesDto>> HandleAsync(GetUsersWithRolesQuery query, CancellationToken cancellationToken = default)
    {
        var users = await _roles.GetUsersWithRolesAsync(cancellationToken);
        return users.Select(UserWithRolesDto.From).ToList();
    }
}
