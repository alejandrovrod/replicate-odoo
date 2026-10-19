using Erp.Domain.Entities.Security;
using Erp.Domain.Exceptions;

namespace Erp.Application.Features.Security.Users;

/// <summary>
/// Command-level validation for <see cref="UpdateUserCommand"/> (spec D4). Returns
/// human-readable violations; the handler maps them to <c>INVALID_USER_DATA</c> (400).
/// </summary>
public static class UpdateUserCommandValidator
{
    public static IReadOnlyList<string> Validate(UpdateUserCommand command)
    {
        var violations = new List<string>();

        if (command.UserId == Guid.Empty)
        {
            violations.Add("A user id is required.");
        }

        try
        {
            // Email is immutable here; validating the full-name rule alone is enough.
            UserValidator.EnsureValidFields(command.FullName, "placeholder@example.com");
        }
        catch (AuthValidationException ex)
        {
            violations.Add(ex.Message);
        }

        if (command.RoleIds is null || command.RoleIds.Count == 0)
        {
            violations.Add("At least one role must be assigned.");
        }

        return violations;
    }
}
