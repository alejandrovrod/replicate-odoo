using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.UnitTests.Fakes;

/// <summary>In-memory <see cref="ICurrencyRepository"/> (RM-09): tests seed the ISO catalog they need.</summary>
public sealed class FakeCurrencyRepository : ICurrencyRepository
{
    private readonly List<Currency> _currencies = new();

    public Currency? AddedCurrency { get; private set; }

    /// <summary>When true, ExistsCodeAsync reports the code as already taken.</summary>
    public bool CodeExists { get; set; }

    public void Seed(params Currency[] currencies) => _currencies.AddRange(currencies);

    public static Currency Usd(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Code = "USD",
        Symbol = "$",
        FractionName = "Cent",
        IsActive = true,
    };

    public Task AddAsync(Currency currency, CancellationToken cancellationToken = default)
    {
        AddedCurrency = currency;
        _currencies.Add(currency);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsCodeAsync(string code, CancellationToken cancellationToken = default)
        => Task.FromResult(CodeExists || _currencies.Any(c => c.Code == code.Trim().ToUpperInvariant()));

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
