using Erp.Application.Features.Selling.Commands;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Task 5.1 acceptance exercised through the CQRS handler: pure Domain validation
/// (CustomerValidator), the duplicate-code-per-COMPANY rule (plan.md §1
/// UQ_Customer_Tenant_Company_Code), the optional receivable-account lookup and persistence
/// (mirrors CreateSupplierCommandHandlerTests / CreateAccountCommandHandlerTests).
/// </summary>
public sealed class CreateCustomerCommandHandlerTests
{
    private static readonly Guid CompanyId = Guid.Parse("22222222-2222-4222-8222-222222222222");

    private readonly FakeCustomerRepository _customers = new();
    private readonly FakeAccountRepository _accounts = new();

    private CreateCustomerCommandHandler CreateHandler() => new(_customers, _accounts, new FakeCurrencyRepository());

    [Fact]
    public async Task HandleAsync_ValidCustomer_PersistsTrimmedCustomerWithPlanDefaults()
    {
        var result = await CreateHandler().HandleAsync(
            new CreateCustomerCommand(CompanyId, "  CUST-001  ", " ACME Corp ", CreditLimit: 5000m));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(CompanyId, result.Value!.CompanyId);
        Assert.Equal("CUST-001", result.Value.Code); // trimmed
        Assert.Equal("ACME Corp", result.Value.Name);
        Assert.Equal(5000m, result.Value.CreditLimit);
        Assert.Equal(0m, result.Value.OutstandingAmount); // starts at zero - only postings move it
        Assert.Null(result.Value.CurrencyId); // no currency linked - reads fall back to "USD"
        Assert.Equal(30, result.Value.PaymentTermsDays);
        Assert.False(result.Value.BypassCreditLimitCheck);
        Assert.True(result.Value.IsActive);

        var saved = _customers.AddedCustomer;
        Assert.NotNull(saved);
        Assert.Equal("CUST-001", saved!.CustomerCode);
        Assert.Equal(Guid.Empty, saved.TenantId); // stamped by AppDbContext, never by the handler
    }

    [Fact]
    public async Task HandleAsync_DuplicateCodeInCompany_FailsWithDuplicateCustomerCode()
    {
        _customers.Seed(new Customer
        {
            Id = Guid.NewGuid(),
            CompanyId = CompanyId,
            CustomerCode = "CUST-001",
            CustomerName = "First Buyer",
        });

        var result = await CreateHandler().HandleAsync(
            new CreateCustomerCommand(CompanyId, "CUST-001", "Second Buyer"));

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(SellingErrorCodes.DuplicateCustomerCode, result.Error!.Code);
        Assert.Null(_customers.AddedCustomer);
    }

    [Fact]
    public async Task HandleAsync_MissingCode_Fails()
    {
        var result = await CreateHandler().HandleAsync(
            new CreateCustomerCommand(CompanyId, "   ", "ACME"));

        Assert.False(result.IsSuccess);
        Assert.Equal(SellingErrorCodes.CustomerCodeRequired, result.Error!.Code);
        Assert.Null(_customers.AddedCustomer);
    }

    [Fact]
    public async Task HandleAsync_MissingName_Fails()
    {
        var result = await CreateHandler().HandleAsync(
            new CreateCustomerCommand(CompanyId, "CUST-001", ""));

        Assert.False(result.IsSuccess);
        Assert.Equal(SellingErrorCodes.CustomerNameRequired, result.Error!.Code);
        Assert.Null(_customers.AddedCustomer);
    }

    [Fact]
    public async Task HandleAsync_NegativeCreditLimit_Fails()
    {
        // CK_Customer_CreditLimit (plan.md §1) keeps the stored value non-negative; the domain
        // rule reports it as an RFC 7807 400 instead of letting the CHECK constraint 500.
        var result = await CreateHandler().HandleAsync(
            new CreateCustomerCommand(CompanyId, "CUST-001", "ACME", CreditLimit: -1m));

        Assert.False(result.IsSuccess);
        Assert.Equal(SellingErrorCodes.InvalidCreditLimit, result.Error!.Code);
        Assert.Null(_customers.AddedCustomer);
    }

    [Fact]
    public async Task HandleAsync_ReceivableAccountOfSameCompany_PersistsLink()
    {
        var accountId = Guid.NewGuid();
        _accounts.Seed(new Account
        {
            Id = accountId,
            CompanyId = CompanyId,
            AccountCode = "1200",
            AccountName = "Accounts Receivable",
        });

        var result = await CreateHandler().HandleAsync(
            new CreateCustomerCommand(CompanyId, "CUST-002", "ACME", DefaultReceivableAccountId: accountId));

        Assert.True(result.IsSuccess);
        Assert.Equal(accountId, result.Value!.DefaultReceivableAccountId);
        Assert.Equal(accountId, _customers.AddedCustomer!.DefaultReceivableAccountId);
    }

    [Fact]
    public async Task HandleAsync_ReceivableAccountOfAnotherCompany_Fails()
    {
        // FK_Customer_Account would otherwise surface as a raw SQL error - the lookup must reject
        // an account outside the customer's company with a stable domain code.
        var foreignAccountId = Guid.NewGuid();
        _accounts.Seed(new Account
        {
            Id = foreignAccountId,
            CompanyId = Guid.NewGuid(),
            AccountCode = "1200",
            AccountName = "Someone Else's Receivable",
        });

        var result = await CreateHandler().HandleAsync(
            new CreateCustomerCommand(CompanyId, "CUST-003", "ACME", DefaultReceivableAccountId: foreignAccountId));

        Assert.False(result.IsSuccess);
        Assert.Equal(SellingErrorCodes.InvalidReceivableAccount, result.Error!.Code);
        Assert.Null(_customers.AddedCustomer);
    }
}
