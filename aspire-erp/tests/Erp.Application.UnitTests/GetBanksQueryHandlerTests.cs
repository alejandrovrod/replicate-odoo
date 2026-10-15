using Erp.Application.Features.Banking.Commands;
using Erp.Application.Features.Banking.Queries;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// bank-catalog spec BNK-G1/G2/G5 + create-with-BankId: the GetBanks query filters by
/// normalized country, autocompletes by code, rejects unknown countries, and the create
/// handler resolves an optional BankId (official name autocomplete, bank_not_found on
/// unknown/inactive, free-text fallback without BankId).
/// </summary>
public sealed class GetBanksQueryHandlerTests
{
    private static readonly Guid CompanyId = Guid.Parse("33333333-3333-4333-8333-333333333333");

    private readonly FakeBankRepository _banks = new();
    private readonly FakeCompanyRepository _companies = new();
    private readonly FakeAccountRepository _accounts = new();
    private readonly FakeCurrencyRepository _currencies = new();

    private readonly Guid _glAccountId = Guid.NewGuid();
    private readonly Guid _bbvaId = Guid.NewGuid();

    public GetBanksQueryHandlerTests()
    {
        _companies.Company = new Company { Id = CompanyId, Name = "Acme MX", CountryCode = "MX" };
        _accounts.Seed(new Account
        {
            Id = _glAccountId,
            CompanyId = CompanyId,
            AccountCode = "1110",
            AccountName = "Cash",
            IsActive = true,
            IsGroup = false,
        });
        _banks.SeedBank(new Bank
        {
            Id = _bbvaId,
            CountryCode = "MX",
            Code = "012",
            Name = "BBVA México",
            SwiftBic = "BCMRMXMM",
            IsActive = true,
        });
        _banks.SeedBank(new Bank
        {
            Id = Guid.NewGuid(),
            CountryCode = "MX",
            Code = "002",
            Name = "Banco Nacional de México (Banamex)",
            IsActive = true,
        });
        _banks.SeedBank(new Bank
        {
            Id = Guid.NewGuid(),
            CountryCode = "MX",
            Code = "999",
            Name = "Inactive Bank",
            IsActive = false,
        });
    }

    private GetBanksQueryHandler QueryHandler() => new(_banks);

    private CreateBankAccountCommandHandler CreateHandler()
        => new(_banks, _companies, _accounts, _currencies);

    [Fact]
    public async Task HandleAsync_MxCountry_ReturnsActiveOrderedByCode()
    {
        var result = await QueryHandler().HandleAsync(new GetBanksQuery("MX"));

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "002", "012" }, result.Value!.Select(b => b.Code));
        Assert.All(result.Value!, b => Assert.Equal("MX", b.CountryCode));
    }

    [Fact]
    public async Task HandleAsync_LowercaseCountry_Normalizes()
    {
        var result = await QueryHandler().HandleAsync(new GetBanksQuery("mx"));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
    }

    [Fact]
    public async Task HandleAsync_Search012_ReturnsBbva()
    {
        var result = await QueryHandler().HandleAsync(new GetBanksQuery("MX", "012"));

        Assert.True(result.IsSuccess);
        var single = Assert.Single(result.Value!);
        Assert.Equal("BBVA México", single.Name);
        Assert.Equal("BCMRMXMM", single.SwiftBic);
    }

    [Fact]
    public async Task HandleAsync_InvalidCountry_FailsWithInvalidBankCountry()
    {
        var result = await QueryHandler().HandleAsync(new GetBanksQuery("XX"));

        Assert.False(result.IsSuccess);
        Assert.Equal(BankingErrorCodes.InvalidBankCountry, result.Error!.Code);
    }

    [Fact]
    public async Task HandleAsync_NullCountry_DefaultsToMx()
    {
        var result = await QueryHandler().HandleAsync(new GetBanksQuery());

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
    }

    [Fact]
    public async Task Create_WithBankIdAndEmptyBankName_AutocompletesOfficialName()
    {
        var result = await CreateHandler().HandleAsync(
            new CreateBankAccountCommand(CompanyId, "Main", "", "004920", _glAccountId, null, true, _bbvaId));

        Assert.True(result.IsSuccess);
        Assert.Equal("BBVA México", result.Value!.BankName);
        Assert.Equal(_bbvaId, result.Value!.BankId);
    }

    [Fact]
    public async Task Create_WithUnknownBankId_FailsWithBankNotFound()
    {
        var result = await CreateHandler().HandleAsync(
            new CreateBankAccountCommand(CompanyId, "Main", "X", "004920", _glAccountId, null, true, Guid.NewGuid()));

        Assert.False(result.IsSuccess);
        Assert.Equal(BankingErrorCodes.BankNotFound, result.Error!.Code);
    }

    [Fact]
    public async Task Create_WithoutBankId_KeepsFreeTextFallback()
    {
        var result = await CreateHandler().HandleAsync(
            new CreateBankAccountCommand(CompanyId, "Main", "Banco Ficticio", "004920", _glAccountId));

        Assert.True(result.IsSuccess);
        Assert.Equal("Banco Ficticio", result.Value!.BankName);
        Assert.Null(result.Value!.BankId);
    }

    [Fact]
    public async Task Create_WithInactiveBankId_FailsWithBankNotFound()
    {
        var inactive = _banks.Banks.Single(b => !b.IsActive);
        var result = await CreateHandler().HandleAsync(
            new CreateBankAccountCommand(CompanyId, "Main", "", "004920", _glAccountId, null, true, inactive.Id));

        Assert.False(result.IsSuccess);
        Assert.Equal(BankingErrorCodes.BankNotFound, result.Error!.Code);
    }

    [Fact]
    public async Task Create_WithWrongCountryBankId_FailsWithBankNotFound()
    {
        var usBankId = Guid.NewGuid();
        _banks.SeedBank(new Bank
        {
            Id = usBankId,
            CountryCode = "US",
            Code = "123456789",
            Name = "US Bank",
            IsActive = true,
        });

        var result = await CreateHandler().HandleAsync(
            new CreateBankAccountCommand(CompanyId, "Main", "", "004920", _glAccountId, null, true, usBankId));

        Assert.False(result.IsSuccess);
        Assert.Equal(BankingErrorCodes.BankNotFound, result.Error!.Code);
    }
}
