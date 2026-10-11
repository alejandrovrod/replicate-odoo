using Erp.Domain.Entities.Security;
using Erp.Domain.Repositories;

namespace Erp.Application.UnitTests.Fakes;

/// <summary>In-memory <see cref="IUserRepository"/> for the profile &amp; security handler tests.</summary>
public sealed class FakeUserRepository : IUserRepository
{
    private readonly List<User> _users = new();
    private readonly List<UserRecoveryCode> _codes = new();

    public void Seed(params User[] users) => _users.AddRange(users);

    public static User WithPassword(Guid? userId = null, Guid? tenantId = null, string passwordHash = "v1.plain")
    {
        var tenant = tenantId ?? Guid.Parse("11111111-1111-4111-8111-111111111111");
        return new User
        {
            Id = userId ?? Guid.NewGuid(),
            TenantId = tenant,
            Email = "user@example.com",
            FullName = "Test User",
            PasswordHash = passwordHash,
        };
    }

    public Task<User?> GetUserByEmailAsync(string email, Guid tenantId, CancellationToken cancellationToken = default)
        => Task.FromResult(_users.FirstOrDefault(u => u.Email == email && u.TenantId == tenantId));

    public Task<List<DocTypePermission>> GetUserPermissionsAsync(Guid userId, CancellationToken cancellationToken = default)
        => Task.FromResult(new List<DocTypePermission>());

    public Task<User?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default)
        => Task.FromResult(_users.FirstOrDefault(u => u.Id == userId));

    public Task UpdateUserAsync(User user, CancellationToken cancellationToken = default)
        => Task.CompletedTask; // entities are held by reference, like a tracked EF graph

    public Task<IReadOnlyList<UserRecoveryCode>> GetActiveRecoveryCodesAsync(Guid userId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<UserRecoveryCode>>(
            _codes.Where(c => c.UserId == userId && c.UsedAt == null).OrderBy(c => c.CreatedAt).ToList());

    public Task ReplaceRecoveryCodesAsync(User user, IReadOnlyList<UserRecoveryCode> codes, CancellationToken cancellationToken = default)
    {
        _codes.RemoveAll(c => c.UserId == user.Id);
        _codes.AddRange(codes);
        return Task.CompletedTask;
    }

    public Task MarkRecoveryCodeUsedAsync(Guid codeId, CancellationToken cancellationToken = default)
    {
        var code = _codes.FirstOrDefault(c => c.Id == codeId);
        if (code is not null && code.UsedAt is null)
        {
            code.UsedAt = DateTimeOffset.UtcNow;
        }

        return Task.CompletedTask;
    }
}
