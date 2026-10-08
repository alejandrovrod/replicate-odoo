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

    public async Task UpdateCompanyAsync(Company company, CancellationToken cancellationToken = default)
    {
        _dbContext.Companies.Update(company);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task EnsurePostingDateInOpenYearAsync(Guid companyId, DateOnly postingDate, CancellationToken cancellationToken = default)
    {
        var covering = await _dbContext.FiscalYears
            .Where(x => x.CompanyId == companyId && x.StartDate <= postingDate && x.EndDate >= postingDate)
            .OrderBy(x => x.StartDate)
            .FirstOrDefaultAsync(cancellationToken);
        covering?.EnsurePostingAllowed(postingDate);
    }
}
