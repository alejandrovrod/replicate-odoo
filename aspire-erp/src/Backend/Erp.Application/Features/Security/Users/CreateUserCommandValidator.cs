using Erp.Domain.Entities.Security;
using Erp.Domain.Exceptions;

namespace Erp.Application.Features.Security.Users;

/// <summary>
/// Command-level validation for <see cref="CreateUserCommand"/> (spec D4). The project keeps
/// Erp.Application free of NuGet packages (Constitution I.3 / decision C2), so this is the
/// in-house equivalent of a FluentValidation validator: it returns human-readable violations
/// and the handler maps any non-empty result to <c>INVALID_USER_DATA</c> (400).
/// </summary>
public static class CreateUserCommandValidator
{
    public static IReadOnlyList<string> Validate(CreateUserCommand command)
    {
        var violations = new List<string>();

        try
        {
            UserValidator.EnsureValidFields(command.FullName, command.Email);
        }
        catch (AuthValidationException ex)
        {
            violations.Add(ex.Message);
        }

        if (command.RoleIds is null || command.RoleIds.Count == 0)
        {
            violations.Add("At least one role must be assigned.");
        }

        if (!string.IsNullOrWhiteSpace(command.TemporaryPassword))
        {
            try
            {
                ProfileValidator.EnsureValidNewPassword(
                    command.TemporaryPassword,
                    command.TemporaryPassword);
            }
            catch (AuthValidationException ex)
            {
                violations.Add(ex.Message);
            }
        }

        return violations;
    }
}
