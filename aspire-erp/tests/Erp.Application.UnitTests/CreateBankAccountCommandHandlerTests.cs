using Erp.Application.Features.Banking.Commands;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Bank account master acceptance exercised through the CQRS handlers: field validation,
/// the company/GL-account/currency lookups, persistence and the RowVersion compare-and-swap
/// on update (mirrors CreateSupplierCommandHandlerTests).
/// </summary>
public sealed class CreateBankAccountCommandHandlerTests
{
    private static readonly Guid CompanyId = Guid.Parse("22222222-2222-4222-8222-222222222222");

    private readonly FakeBankRepository _banks = new();
    private readonly FakeCompanyRepository _companies = new();
    private readonly FakeAccountRepository _accounts = new();
    private readonly FakeCurrencyRepository _currencies = new();

    private readonly Guid _glAccountId = Guid.NewGuid();
    private readonly Guid _currencyId = Guid.NewGuid();

    public CreateBankAccountCommandHandlerTests()
    {
        _companies.Company = new Company { Id = CompanyId, Name = "Acme" };
        _accounts.Seed(new Account
        {
            Id = _glAccountId,
            CompanyId = CompanyId,
            AccountCode = "1110",
            AccountName = "Cash",
            IsActive = true,
            IsGroup = false,
        });
        _currencies.Seed(FakeCurrencyRepository.Usd(_currencyId));
    }

    private CreateBankAccountCommandHandler CreateHandler()
        => new(_banks, _companies, _accounts, _currencies);

    private UpdateBankAccountCommandHandler UpdateHandler()
        => new(_banks, _accounts, _currencies);

    [Fact]
    public async Task HandleAsync_ValidAccount_PersistsAndReturnsDto()
    {
        var result = await CreateHandler().HandleAsync(
            new CreateBankAccountCommand(CompanyId, "Main Account", "JPMorgan", "004920", _glAccountId, _currencyId));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Main Account", result.Value!.AccountName);
        Assert.Equal(_glAccountId, result.Value.GLAccountId);
        Assert.Equal(_currencyId, result.Value.CurrencyId);
        Assert.Equal(0m, result.Value.LastReconciledBalance);
        Assert.True(result.Value.IsActive);

        Assert.NotNull(_banks.AddedAccount);
    }

    [Fact]
    public async Task HandleAsync_UnknownGlAccount_Fails()
    {
        var result = await CreateHandler().HandleAsync(
            new CreateBankAccountCommand(CompanyId, "Main", "Bank", "001", Guid.NewGuid()));

        Assert.False(result.IsSuccess);
        Assert.Equal(BankingErrorCodes.InvalidGlAccount, result.Error!.Code);
        Assert.Null(_banks.AddedAccount);
    }

    [Fact]
    public async Task HandleAsync_UnknownCurrency_Fails()
    {
        var result = await CreateHandler().HandleAsync(
            new CreateBankAccountCommand(CompanyId, "Main", "Bank", "001", _glAccountId, Guid.NewGuid()));

        Assert.False(result.IsSuccess);
        Assert.Equal(CurrencyErrorCodes.CurrencyNotFound, result.Error!.Code);
    }

    [Fact]
    public async Task HandleAsync_BlankName_Fails()
    {
        var result = await CreateHandler().HandleAsync(
            new CreateBankAccountCommand(CompanyId, "   ", "Bank", "001", _glAccountId));

        Assert.False(result.IsSuccess);
        Assert.Equal(BankingErrorCodes.BankAccountNameRequired, result.Error!.Code);
    }

    [Fact]
    public async Task UpdateAsync_StaleRowVersion_FailsWithConcurrencyConflict()
    {
        var account = new BankAccount
        {
            Id = Guid.NewGuid(),
            CompanyId = CompanyId,
            AccountName = "Main",
            BankName = "Bank",
            AccountNumber = "001",
            GLAccountId = _glAccountId,
            RowVersion = new byte[] { 1, 2, 3, 4 },
        };
        _banks.SeedAccount(account);

        var result = await UpdateHandler().HandleAsync(
            new UpdateBankAccountCommand(
                account.Id, CompanyId, "Main", "Bank", "001", _glAccountId, null, true,
                new byte[] { 9, 9, 9, 9 }));

        Assert.False(result.IsSuccess);
        Assert.Equal("concurrency_conflict", result.Error!.Code);
    }

    [Fact]
    public async Task UpdateAsync_MatchingRowVersion_PersistsChanges()
    {
        var rowVersion = new byte[] { 1, 2, 3, 4 };
        var account = new BankAccount
        {
            Id = Guid.NewGuid(),
            CompanyId = CompanyId,
            AccountName = "Main",
            BankName = "Bank",
            AccountNumber = "001",
            GLAccountId = _glAccountId,
            RowVersion = rowVersion,
        };
        _banks.SeedAccount(account);

        var result = await UpdateHandler().HandleAsync(
            new UpdateBankAccountCommand(
                account.Id, CompanyId, "Renamed", "Bank", "001", _glAccountId, _currencyId, false,
                rowVersion));

        Assert.True(result.IsSuccess);
        Assert.Equal("Renamed", result.Value!.AccountName);
        Assert.Equal(_currencyId, result.Value.CurrencyId);
        Assert.False(result.Value.IsActive);
    }
}
