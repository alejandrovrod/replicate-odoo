using Erp.Domain.Entities;
using Erp.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Data.Repositories;

/// <summary>EF Core implementation of <see cref="ICompanyRepository"/> (tenant filter automatic).</summary>
public sealed class CompanyRepository : ICompanyRepository
{
    private readonly AppDbContext _dbContext;

    public CompanyRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Company?> GetByIdAsync(Guid companyId, CancellationToken cancellationToken = default)
        => _dbContext.Companies.FirstOrDefaultAsync(c => c.Id == companyId, cancellationToken);
}
