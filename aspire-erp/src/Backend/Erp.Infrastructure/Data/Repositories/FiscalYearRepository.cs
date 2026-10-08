using System.Data;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Data.Repositories;

/// <summary>
/// EF implementation of <see cref="IFiscalYearRepository"/> (R-13). Tenant isolation is automatic
/// via the <see cref="AppDbContext"/> global query filter.
/// </summary>
public sealed class FiscalYearRepository : IFiscalYearRepository
{
    private readonly AppDbContext _context;

    public FiscalYearRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<FiscalYear?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.FiscalYears.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<FiscalYear?> GetCoveringYearAsync(Guid companyId, DateOnly date, CancellationToken cancellationToken = default) =>
        _context.FiscalYears
            .Where(x => x.CompanyId == companyId && x.StartDate <= date && x.EndDate >= date)
            .OrderBy(x => x.StartDate)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> HasOverlapAsync(Guid companyId, DateOnly startDate, DateOnly endDate, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.FiscalYears
            .Where(x => x.CompanyId == companyId && x.StartDate <= endDate && startDate <= x.EndDate);

        if (excludeId.HasValue)
        {
            query = query.Where(x => x.Id != excludeId.Value);
        }

        return query.AnyAsync(cancellationToken);
    }

    public async Task AddAsync(FiscalYear year, CancellationToken cancellationToken = default) =>
        await _context.FiscalYears.AddAsync(year, cancellationToken);

    public void Update(FiscalYear year) => _context.FiscalYears.Update(year);

    public Task<List<FiscalYear>> GetPagedAsync(Guid companyId, bool? isClosed, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = _context.FiscalYears.Where(x => x.CompanyId == companyId);
        if (isClosed.HasValue)
        {
            query = query.Where(x => x.IsClosed == isClosed.Value);
        }

        return query.OrderBy(x => x.StartDate).Skip(skip).Take(take).ToListAsync(cancellationToken);
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
    {
        if (_context.Database.CurrentTransaction != null)
        {
            return await operation();
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await operation();
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<T> ExecuteInSerializableTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
    {
        if (_context.Database.CurrentTransaction != null)
        {
            return await operation();
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var result = await operation();
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task EnsurePostingDateInOpenYearAsync(Guid companyId, DateOnly postingDate, CancellationToken cancellationToken = default)
    {
        var covering = await GetCoveringYearAsync(companyId, postingDate, cancellationToken);
        covering?.EnsurePostingAllowed(postingDate);
    }
}
