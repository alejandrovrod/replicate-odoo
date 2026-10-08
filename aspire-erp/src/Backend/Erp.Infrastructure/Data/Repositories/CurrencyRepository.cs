using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ICurrencyRepository"/> (RM-09).
/// </summary>
/// <remarks>
/// Currencies are GLOBAL (no TenantId, no query filter): every read sees the full ISO catalog.
/// Code comparisons are case-insensitive (ISO codes are stored uppercase).
/// </remarks>
public sealed class CurrencyRepository : ICurrencyRepository
{
    private readonly AppDbContext _dbContext;

    public CurrencyRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(Currency currency, CancellationToken cancellationToken = default)
    {
        await _dbContext.Currencies.AddAsync(currency, cancellationToken);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // UQ_Currency_Code is the hard backstop of the duplicate-code rule: the handler's
            // ExistsCodeAsync pre-check can be raced by a concurrent insert, so translate the
            // race into the same domain failure instead of leaking an EF/SQL exception.
            _dbContext.Entry(currency).State = EntityState.Detached;
            throw new CurrencyValidationException(
                CurrencyErrorCodes.DuplicateCurrencyCode,
                $"Currency code '{currency.Code}' already exists.");
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };

    public Task<bool> ExistsCodeAsync(string code, CancellationToken cancellationToken = default)
        => _dbContext.Currencies.AnyAsync(
            c => c.Code == code.Trim().ToUpperInvariant(),
            cancellationToken);

    public Task<Currency?> GetByIdAsync(Guid currencyId, CancellationToken cancellationToken = default)
        => _dbContext.Currencies.FirstOrDefaultAsync(c => c.Id == currencyId, cancellationToken);

    public Task<Currency?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return _dbContext.Currencies.FirstOrDefaultAsync(c => c.Code == normalized, cancellationToken);
    }

    public async Task<IReadOnlyList<Currency>> GetAllAsync(bool onlyActive, CancellationToken cancellationToken = default)
        => await _dbContext.Currencies
            .Where(c => !onlyActive || c.IsActive)
            .OrderBy(c => c.Code)
            .ToListAsync(cancellationToken);

    public async Task UpdateAsync(Currency currency, CancellationToken cancellationToken = default)
    {
        _dbContext.Currencies.Update(currency);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _dbContext.Entry(currency).State = EntityState.Detached;
            throw new ConcurrencyConflictException(nameof(Currency), currency.Id, ex);
        }
    }
}
