using Erp.Application.Common;
using Erp.Domain.Entities.Security;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Security.Roles;

/// <summary>
/// Creates a functional role (SEC-G2): <c>201</c> with <c>IsSystemDefault = false</c>.
/// A duplicate name in the SAME tenant is <c>409 ROLE_NAME_CONFLICT</c>; the same name
/// in ANOTHER tenant is legal (UQ includes <c>TenantId</c>).
/// </summary>
public sealed record CreateRoleCommand(string? Name, string? Description, Guid TenantId)
    : ICommand<Result<RoleDto>>;

public sealed class CreateRoleCommandHandler : ICommandHandler<CreateRoleCommand, Result<RoleDto>>
{
    private readonly IRoleRepository _roles;

    public CreateRoleCommandHandler(IRoleRepository roles)
    {
        _roles = roles;
    }

    public async Task<Result<RoleDto>> HandleAsync(CreateRoleCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            var normalized = RoleValidator.EnsureValidName(command.Name);

            var existing = await _roles.GetRoleByNameAsync(normalized, cancellationToken);
            RoleValidator.EnsureNameAvailable(normalized, existing is not null);

            var role = new Role
            {
                Id = Guid.NewGuid(),
                TenantId = command.TenantId,
                Name = normalized,
                Description = command.Description?.Trim() ?? string.Empty,
                IsSystemDefault = false,
            };

            await _roles.CreateRoleAsync(role, cancellationToken);

            return Result<RoleDto>.Success(RoleDto.From(role, 0));
        }
        catch (AuthValidationException ex)
        {
            return Result<RoleDto>.Failure(ex.Code, ex.Message);
        }
    }
}
