using Erp.Application.Common;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Security.Commands;

public record LoginCommand(string Email, string Password, Guid TenantId) : ICommand<Result<LoginResponseDto>>;

public record LoginResponseDto(string Token, string FullName, string Email);

public class LoginCommandHandler : ICommandHandler<LoginCommand, Result<LoginResponseDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly ITokenGenerator _tokenGenerator;

    public LoginCommandHandler(IUserRepository userRepository, ITokenGenerator tokenGenerator)
    {
        _userRepository = userRepository;
        _tokenGenerator = tokenGenerator;
    }

    public async Task<Result<LoginResponseDto>> HandleAsync(LoginCommand request, CancellationToken cancellationToken)
    {
        // Notice we explicitly pass TenantId to cross the DB filter if needed, 
        // though normally TenantResolutionMiddleware would set it. 
        // For a public login endpoint, it might be set via header.
        var user = await _userRepository.GetUserByEmailAsync(request.Email, request.TenantId, cancellationToken);
        if (user == null)
        {
            return Result<LoginResponseDto>.Failure("Auth.Failed", "Invalid credentials.");
        }

        // In a real scenario, use proper Password Hash verification (e.g., BCrypt or Identity PasswordHasher).
        // For this milestone we match in plain text or simple hash as imported from legacy systems.
        if (user.PasswordHash != request.Password)
        {
            return Result<LoginResponseDto>.Failure("Auth.Failed", "Invalid credentials.");
        }

        var permissions = await _userRepository.GetUserPermissionsAsync(user.Id, cancellationToken);
        var roles = user.UserRoles
            .Where(ur => ur.Role is not null)
            .Select(ur => ur.Role.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var token = _tokenGenerator.GenerateToken(user, permissions, roles);

        return Result<LoginResponseDto>.Success(new LoginResponseDto(token, user.FullName, user.Email));
    }
}
