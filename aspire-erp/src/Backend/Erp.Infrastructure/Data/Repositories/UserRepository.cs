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

    public async Task<User?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Tenant isolation is AUTOMATIC through the global query filter (Constitution II.3):
        // a user of another tenant is simply not found here.
        return await _context.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
    }

    public async Task UpdateUserAsync(User user, CancellationToken cancellationToken = default)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UserRecoveryCode>> GetActiveRecoveryCodesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.UserRecoveryCodes
            .Where(c => c.UserId == userId && c.UsedAt == null)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task ReplaceRecoveryCodesAsync(User user, IReadOnlyList<UserRecoveryCode> codes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(codes);

        var existing = await _context.UserRecoveryCodes
            .Where(c => c.UserId == user.Id)
            .ToListAsync(cancellationToken);

        if (existing.Count > 0)
        {
            _context.UserRecoveryCodes.RemoveRange(existing);
        }

        foreach (var code in codes)
        {
            code.UserId = user.Id;
            code.TenantId = user.TenantId;
            await _context.UserRecoveryCodes.AddAsync(code, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkRecoveryCodeUsedAsync(Guid codeId, CancellationToken cancellationToken = default)
    {
        var code = await _context.UserRecoveryCodes
            .FirstOrDefaultAsync(c => c.Id == codeId, cancellationToken);

        if (code is null || code.UsedAt.HasValue)
        {
            return;
        }

        code.UsedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }
}
