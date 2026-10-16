using Erp.Application.Features.Buying.Commands;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Task 4.1 acceptance ("Suppliers validate unique CODE per tenant") exercised through the CQRS
/// handler: pure Domain validation, trimming and the duplicate-code rule surfacing as a domain
/// failure the API maps to 409 (mirrors CreateItemCommandHandlerTests). Extended for the module
/// buying-suppliers governance: group resolution, per-company accounts (spec §1 gaps D1/D2) and
/// the hold setup (gap D4).
/// </summary>
public sealed class CreateSupplierCommandHandlerTests
{
    private readonly FakeSupplierRepository _suppliers = new();
    private readonly FakeSupplierGroupRepository _groups = new();
    private readonly FakeCompanyRepository _companies = new();
    private readonly FakeAccountRepository _accounts = new();

    private CreateSupplierCommandHandler CreateHandler() => new(_suppliers, new FakeCurrencyRepository());

    private CreateSupplierCommandHandler CreateGovernanceHandler(IAccountRepository? accounts = null) =>
        new(_suppliers, new FakeCurrencyRepository(), _groups, _companies, accounts);

    [Fact]
    public async Task HandleAsync_ValidSupplier_PersistsTrimmedSupplierAndReturnsDto()
    {
        var result = await CreateHandler().HandleAsync(
            new CreateSupplierCommand("  SUP-001  ", " Acme Industrial Supplies "));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("SUP-001", result.Value!.Code); // trimmed
        Assert.Equal("Acme Industrial Supplies", result.Value.Name);
        Assert.True(result.Value.IsActive);

        var saved = _suppliers.AddedSupplier;
        Assert.NotNull(saved);
        Assert.Equal("SUP-001", saved!.Code);
        Assert.Equal(Guid.Empty, saved.TenantId); // stamped by AppDbContext, never by the handler
    }

    [Fact]
    public async Task HandleAsync_DuplicateCodeInTenant_FailsWithDuplicateSupplierCode()
    {
        _suppliers.Seed(new Supplier
        {
            Id = Guid.NewGuid(),
            Code = "SUP-001",
            Name = "First Vendor",
        });

        var result = await CreateHandler().HandleAsync(
            new CreateSupplierCommand("SUP-001", "Second Vendor"));

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(PurchaseErrorCodes.DuplicateSupplierCode, result.Error!.Code);
        Assert.Null(_suppliers.AddedSupplier);
    }

    [Fact]
    public async Task HandleAsync_MissingCode_Fails()
    {
        var result = await CreateHandler().HandleAsync(
            new CreateSupplierCommand("   ", "Acme"));

        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseErrorCodes.SupplierCodeRequired, result.Error!.Code);
        Assert.Null(_suppliers.AddedSupplier);
    }

    [Fact]
    public async Task HandleAsync_MissingName_Fails()
    {
        var result = await CreateHandler().HandleAsync(
            new CreateSupplierCommand("SUP-001", ""));

        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseErrorCodes.SupplierNameRequired, result.Error!.Code);
        Assert.Null(_suppliers.AddedSupplier);
    }

    [Fact]
    public async Task HandleAsync_InactiveSupplier_CanStillBeCreated()
    {
        var result = await CreateHandler().HandleAsync(
            new CreateSupplierCommand("SUP-002", "Blocked Vendor", IsActive: false));

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.IsActive);
        Assert.False(_suppliers.AddedSupplier!.IsActive);
    }

    // ------------------------------------------------------------- group + multi-company (spec §1)

    [Fact]
    public async Task HandleAsync_WithGroupAndCompanyAccounts_PersistsGroupAndPerCompanyRows()
    {
        var group = new SupplierGroup { Id = Guid.NewGuid(), Code = "RAW", Name = "Raw Materials" };
        _groups.Seed(group);
        _companies.Company = new Company { Id = Guid.NewGuid(), Name = "COMP-001" };

        var companyA = Guid.NewGuid();
        var companyB = Guid.NewGuid();
        var payableA = Guid.NewGuid();
        var payableB = Guid.NewGuid();
        var advanceA = Guid.NewGuid();

        var result = await CreateGovernanceHandler().HandleAsync(
            new CreateSupplierCommand(
                "SUP-2026-001",
                "Tech Supplies SA",
                SupplierGroupId: group.Id,
                CompanyAccounts: new[]
                {
                    new SupplierCompanyAccountInput(companyA, payableA, advanceA),
                    new SupplierCompanyAccountInput(companyB, payableB),
                }));

        Assert.True(result.IsSuccess);
        var saved = _suppliers.AddedSupplier!;
        Assert.Equal(group.Id, saved.SupplierGroupId);
        Assert.Equal(2, saved.Accounts.Count);
        Assert.Contains(saved.Accounts, a => a.CompanyId == companyA && a.PayableAccountId == payableA && a.AdvanceAccountId == advanceA);
        Assert.Contains(saved.Accounts, a => a.CompanyId == companyB && a.PayableAccountId == payableB && a.AdvanceAccountId is null);
    }

    [Fact]
    public async Task HandleAsync_UnknownGroup_FailsWithSupplierGroupNotFound()
    {
        var result = await CreateGovernanceHandler().HandleAsync(
            new CreateSupplierCommand("SUP-2026-002", "Acme", SupplierGroupId: Guid.NewGuid()));

        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseErrorCodes.SupplierGroupNotFound, result.Error!.Code);
        Assert.Null(_suppliers.AddedSupplier);
    }

    [Fact]
    public async Task HandleAsync_DuplicateCompanyInAccounts_FailsWithInvalidGlAccount()
    {
        var duplicate = Guid.NewGuid();
        // The company must exist so validation reaches the duplicate rule instead of
        // short-circuiting on the company-existence check (which yields CompanyNotFound).
        _companies.Company = new Company { Id = duplicate, Name = "COMP-001" };

        var result = await CreateGovernanceHandler().HandleAsync(
            new CreateSupplierCommand("SUP-2026-003", "Acme", CompanyAccounts: new[]
            {
                new SupplierCompanyAccountInput(duplicate, Guid.NewGuid()),
                new SupplierCompanyAccountInput(duplicate, Guid.NewGuid()),
            }));

        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseErrorCodes.InvalidGlAccount, result.Error!.Code);
        Assert.Null(_suppliers.AddedSupplier);
    }

    [Fact]
    public async Task HandleAsync_UnknownPayableAccount_FailsWithInvalidGlAccount()
    {
        _companies.Company = new Company { Id = Guid.NewGuid(), Name = "COMP-001" };

        var result = await CreateGovernanceHandler(_accounts).HandleAsync(
            new CreateSupplierCommand("SUP-2026-004", "Acme", CompanyAccounts: new[]
            {
                new SupplierCompanyAccountInput(Guid.NewGuid(), Guid.NewGuid()),
            }));

        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseErrorCodes.InvalidGlAccount, result.Error!.Code);
        Assert.Null(_suppliers.AddedSupplier);
    }

    [Fact]
    public async Task HandleAsync_OnHold_PersistsHoldStatusAndReleaseDate()
    {
        var release = new DateOnly(2026, 12, 31);

        var result = await CreateGovernanceHandler().HandleAsync(
            new CreateSupplierCommand(
                "SUP-2026-005",
                "Acme",
                OnHold: true,
                HoldType: SupplierHoldType.Invoices,
                ReleaseDate: release));

        Assert.True(result.IsSuccess);
        var saved = _suppliers.AddedSupplier!;
        Assert.True(saved.OnHold);
        Assert.Equal(SupplierHoldType.Invoices, saved.HoldType);
        Assert.Equal(release, saved.ReleaseDate);
    }
}

