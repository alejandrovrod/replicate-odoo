using Erp.Domain.Entities.Security;
using Erp.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Data.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetUserByEmailAsync(string email, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .IgnoreQueryFilters() // Explicitly query ignoring global filter, matching by TenantId
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email == email && u.TenantId == tenantId, cancellationToken);
    }

    public async Task<List<DocTypePermission>> GetUserPermissionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var roleIds = await _context.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.RoleId)
            .ToListAsync(cancellationToken);

        return await _context.DocTypePermissions
            .Where(dp => roleIds.Contains(dp.RoleId))
            .ToListAsync(cancellationToken);
    }
}
