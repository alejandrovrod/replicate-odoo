using Erp.Application.Common;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Security.Roles;

/// <summary>Lists every role of the current tenant with headcounts (isolation via query filter).</summary>
public sealed record GetRolesQuery : IQuery<IReadOnlyList<RoleDto>>;

public sealed class GetRolesQueryHandler : IQueryHandler<GetRolesQuery, IReadOnlyList<RoleDto>>
{
    private readonly IRoleRepository _roles;

    public GetRolesQueryHandler(IRoleRepository roles)
    {
        _roles = roles;
    }

    public async Task<IReadOnlyList<RoleDto>> HandleAsync(GetRolesQuery query, CancellationToken cancellationToken = default)
    {
        var roles = await _roles.GetRolesWithPermissionsAsync(cancellationToken);
        var counts = await _roles.GetUserCountsByRoleAsync(cancellationToken);

        return roles
            .Select(r => RoleDto.From(r, counts.TryGetValue(r.Id, out var c) ? c : 0))
            .ToList();
    }
}
