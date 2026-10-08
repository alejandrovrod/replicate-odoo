using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Domain.UnitTests;

/// <summary>Minimal in-memory <see cref="ICurrencyRepository"/> (RM-09): unknown ids read as missing.</summary>
public sealed class FakeCurrencyRepository : ICurrencyRepository
{
    private readonly List<Currency> _currencies = new();

    public void Seed(params Currency[] currencies) => _currencies.AddRange(currencies);

    public Task AddAsync(Currency currency, CancellationToken cancellationToken = default)
    {
        _currencies.Add(currency);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsCodeAsync(string code, CancellationToken cancellationToken = default)
        => Task.FromResult(_currencies.Any(c => c.Code == code.Trim().ToUpperInvariant()));

    public Task<Currency?> GetByIdAsync(Guid currencyId, CancellationToken cancellationToken = default)
        => Task.FromResult(_currencies.FirstOrDefault(c => c.Id == currencyId));

    public Task<Currency?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return Task.FromResult(_currencies.FirstOrDefault(c => c.Code == normalized));
    }

    public Task<IReadOnlyList<Currency>> GetAllAsync(bool onlyActive, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Currency>>(
            _currencies.Where(c => !onlyActive || c.IsActive).OrderBy(c => c.Code).ToList());

    public Task UpdateAsync(Currency currency, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
