using Erp.Application.Common;
using Erp.Application.Services;
using Erp.Domain.Entities.Security;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Security.Users;

/// <summary>
/// Executes <see cref="CreateUserCommand"/>: field/role validation, tenant-scoped email
/// uniqueness, PBKDF2 password hashing (generated when no temporary password is supplied) and
/// atomic persistence of the user plus role assignments.
/// </summary>
public sealed class CreateUserCommandHandler
    : ICommandHandler<CreateUserCommand, Result<CreateUserResult>>
{
    private readonly IUserAdminRepository _users;
    private readonly IPasswordHasher _passwords;

    public CreateUserCommandHandler(IUserAdminRepository users, IPasswordHasher passwords)
    {
        _users = users;
        _passwords = passwords;
    }

    public async Task<Result<CreateUserResult>> HandleAsync(
        CreateUserCommand command,
        CancellationToken cancellationToken = default)
    {
        var violations = CreateUserCommandValidator.Validate(command);
        if (violations.Count > 0)
        {
            return Result<CreateUserResult>.Failure(
                AuthErrorCodes.InvalidUserData,
                string.Join(" ", violations));
        }

        var email = UserValidator.NormalizeEmail(command.Email);

        try
        {
            if (await _users.EmailExistsAsync(email, excludeUserId: null, cancellationToken))
            {
                UserValidator.EnsureEmailAvailable(existsInTenant: true, email);
            }

            var roleIds = command.RoleIds.Distinct().ToList();
            var roles = await _users.GetRolesByIdsAsync(roleIds, cancellationToken);
            if (roles.Count != roleIds.Count)
            {
                return Result<CreateUserResult>.Failure(
                    AuthErrorCodes.InvalidUserData,
                    "One or more roles do not belong to this tenant.");
            }

            var plaintextPassword = string.IsNullOrWhiteSpace(command.TemporaryPassword)
                ? TemporaryPasswordGenerator.Generate()
                : command.TemporaryPassword;

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                FullName = command.FullName.Trim(),
                PasswordHash = _passwords.Hash(plaintextPassword),
                IsActive = command.IsActive,
                CreatedAt = DateTime.UtcNow,
            };

            await _users.AddAsync(user, roleIds, cancellationToken);

            var created = await _users.GetByIdWithRolesAsync(user.Id, cancellationToken);
            return Result<CreateUserResult>.Success(
                new CreateUserResult(UserAdminDto.From(created ?? user), plaintextPassword));
        }
        catch (AuthValidationException ex)
        {
            return Result<CreateUserResult>.Failure(ex.Code, ex.Message);
        }
    }
}
