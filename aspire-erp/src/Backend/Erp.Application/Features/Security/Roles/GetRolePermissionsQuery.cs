using Erp.Application.Common;
using Erp.Domain.Entities.Security;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Security.Roles;

/// <summary>Role detail with its permission matrix (SEC-G1). Cross-tenant ids are simply not found (404).</summary>
public sealed record GetRolePermissionsQuery(Guid RoleId) : IQuery<Result<RolePermissionsDto>>;

public sealed class GetRolePermissionsQueryHandler : IQueryHandler<GetRolePermissionsQuery, Result<RolePermissionsDto>>
{
    private readonly IRoleRepository _roles;

    public GetRolePermissionsQueryHandler(IRoleRepository roles)
    {
        _roles = roles;
    }

    public async Task<Result<RolePermissionsDto>> HandleAsync(GetRolePermissionsQuery query, CancellationToken cancellationToken = default)
    {
        var role = await _roles.GetRoleByIdAsync(query.RoleId, cancellationToken);
        if (role is null)
        {
            return Result<RolePermissionsDto>.Failure(AuthErrorCodes.RoleNotFound, "Role not found.");
        }

        return Result<RolePermissionsDto>.Success(RolePermissionsDto.From(role));
    }
}
