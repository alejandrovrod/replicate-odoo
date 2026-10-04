using System;
using System.Threading;
using System.Threading.Tasks;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Data.Repositories;

public sealed class POSProfileRepository : IPOSProfileRepository
{
    private readonly AppDbContext _context;

    public POSProfileRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<POSProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.POSProfiles.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }
}
