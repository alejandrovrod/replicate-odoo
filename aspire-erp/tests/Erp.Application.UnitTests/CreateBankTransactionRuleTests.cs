using Erp.Application.Features.Banking.Commands;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Rule management (task 6.3, Block C read/write): creating scoped and global rules, and the
/// typed rejections (unknown condition, blank pattern, bad regex, unparsable amount,
/// foreign account) with zero persisted rules.
/// </summary>
public sealed class CreateBankTransactionRuleTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _accountId = Guid.NewGuid();
    private readonly FakeBankRepository _bank = new();

    public CreateBankTransactionRuleTests()
    {
        _bank.SeedAccount(new BankAccount
        {
            Id = _accountId,
            TenantId = _tenantId,
            CompanyId = _companyId,
            AccountName = "Main Checking",
            BankName = "Acme Bank",
            AccountNumber = "0012345678",
            GLAccountId = Guid.NewGuid(),
        });
    }

    private CreateBankTransactionRuleCommandHandler Handler() => new(_bank);

    private CreateBankTransactionRuleCommand Scoped(
        string condition = "Contains",
        string pattern = "STRIPE") =>
        new(
            _companyId,
            "Stripe payouts",
            1,
            _accountId,
            condition,
            pattern,
            "Customer",
            Guid.NewGuid(),
            false,
            Guid.NewGuid(),
            true);

    [Fact]
    public async Task Create_ScopedRule_PersistsWithTenantFromAccount()
    {
        var result = await Handler().HandleAsync(Scoped(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var dto = result.Value!;
        Assert.Equal(_companyId, dto.CompanyId);
        Assert.Equal(_accountId, dto.BankAccountId);
        Assert.Equal("Contains", dto.ConditionType);
        Assert.Equal("STRIPE", dto.Pattern);
        Assert.True(dto.IsActive);

        var stored = Assert.Single(_bank.Rules);
        Assert.Equal(_tenantId, stored.TenantId);
    }

    [Fact]
    public async Task Create_GlobalRule_PersistsWithNullAccount()
    {
        var command = Scoped() with { BankAccountId = null };

        var result = await Handler().HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.BankAccountId);
        Assert.Equal(_tenantId, Assert.Single(_bank.Rules).TenantId);
    }

    [Fact]
    public async Task Create_UnknownCondition_FailsWithInvalidRulePatternAndPersistsNothing()
    {
        var result = await Handler().HandleAsync(
            Scoped(condition: "FuzzyMatch"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BankingErrorCodes.InvalidRulePattern, result.Error!.Code);
        Assert.Empty(_bank.Rules);
    }

    [Fact]
    public async Task Create_BlankPattern_FailsWithInvalidRulePatternAndPersistsNothing()
    {
        var result = await Handler().HandleAsync(
            Scoped(pattern: "  "), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BankingErrorCodes.InvalidRulePattern, result.Error!.Code);
        Assert.Empty(_bank.Rules);
    }

    [Fact]
    public async Task Create_InvalidRegex_FailsWithInvalidRulePatternAndPersistsNothing()
    {
        var result = await Handler().HandleAsync(
            Scoped(condition: "RegexMatch", pattern: "([unclosed"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BankingErrorCodes.InvalidRulePattern, result.Error!.Code);
        Assert.Empty(_bank.Rules);
    }

    [Fact]
    public async Task Create_UnparsableAmount_FailsWithInvalidRulePatternAndPersistsNothing()
    {
        var result = await Handler().HandleAsync(
            Scoped(condition: "AmountEquals", pattern: "lots"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BankingErrorCodes.InvalidRulePattern, result.Error!.Code);
        Assert.Empty(_bank.Rules);
    }

    [Fact]
    public async Task Create_AccountOfAnotherCompany_FailsWithBankAccountNotFoundAndPersistsNothing()
    {
        var command = Scoped() with { BankAccountId = Guid.NewGuid() };

        var result = await Handler().HandleAsync(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BankingErrorCodes.BankAccountNotFound, result.Error!.Code);
        Assert.Empty(_bank.Rules);
    }
}
