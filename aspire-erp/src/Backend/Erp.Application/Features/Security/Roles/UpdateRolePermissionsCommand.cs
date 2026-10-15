using Erp.Application.Common;
using Erp.Domain.Entities.Security;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Security.Roles;

/// <summary>
/// Replaces the permission matrix of a functional role (SEC-G1): <c>200</c> with the
/// persisted matrix. System Manager owns no rows - any update over it is
/// <c>403 SYSTEM_MANAGER_IMMUTABLE</c>. Unknown DocTypes and
/// <c>Create</c>-without-<c>Write</c> rows are <c>400</c> (explicit, never coerced).
/// </summary>
public sealed record UpdateRolePermissionsCommand(
    Guid RoleId,
    Guid TenantId,
    IReadOnlyList<DocTypePermissionDto> Permissions) : ICommand<Result<RolePermissionsDto>>;

public sealed class UpdateRolePermissionsCommandHandler
    : ICommandHandler<UpdateRolePermissionsCommand, Result<RolePermissionsDto>>
{
    private readonly IRoleRepository _roles;

    public UpdateRolePermissionsCommandHandler(IRoleRepository roles)
    {
        _roles = roles;
    }

    public async Task<Result<RolePermissionsDto>> HandleAsync(
        UpdateRolePermissionsCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            var role = (await _roles.GetRolesByIdsUnfilteredAsync(
                new[] { command.RoleId }, cancellationToken)).FirstOrDefault();

            if (role is null)
            {
                return Result<RolePermissionsDto>.Failure(AuthErrorCodes.RoleNotFound, "Role not found.");
            }

            if (role.TenantId != command.TenantId)
            {
                return Result<RolePermissionsDto>.Failure(
                    AuthErrorCodes.CrossTenantAssignmentDenied,
                    "The role belongs to another tenant.");
            }

            var rows = (command.Permissions ?? Array.Empty<DocTypePermissionDto>())
                .Select(dto => dto.ToEntity(command.TenantId, role.Id))
                .ToList();

            RoleValidator.EnsurePermissionsMatrix(role, rows, DocTypeCatalog.AllDocTypes);

            await _roles.UpdatePermissionsMatrixAsync(role.Id, rows, cancellationToken);

            role.Permissions = rows;
            return Result<RolePermissionsDto>.Success(RolePermissionsDto.From(role));
        }
        catch (AuthValidationException ex)
        {
            return Result<RolePermissionsDto>.Failure(ex.Code, ex.Message);
        }
    }
}
