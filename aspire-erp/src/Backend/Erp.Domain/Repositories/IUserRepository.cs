using Erp.Domain.Entities.Security;

namespace Erp.Domain.Repositories;

public interface IUserRepository
{
    Task<User?> GetUserByEmailAsync(string email, Guid tenantId, CancellationToken cancellationToken = default);
    Task<List<DocTypePermission>> GetUserPermissionsAsync(Guid userId, CancellationToken cancellationToken = default);
}
