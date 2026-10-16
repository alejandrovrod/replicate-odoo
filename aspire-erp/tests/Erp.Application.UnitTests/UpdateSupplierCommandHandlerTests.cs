using Erp.Application.Features.Buying.Commands;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Module buying-suppliers (tasks.md T2.1, spec scenario SUP-G4): the update/hold CQRS handlers.
/// The optimistic-concurrency gate is a compare-and-swap on <see cref="Supplier.RowVersion"/> that
/// the API maps to 409, so a stale token must fail fast without mutating the row.
/// </summary>
public sealed class UpdateSupplierCommandHandlerTests
{
    private readonly FakeSupplierRepository _suppliers = new();
    private readonly FakeSupplierGroupRepository _groups = new();
    private readonly FakeCompanyRepository _companies = new();
    private readonly FakeAccountRepository _accounts = new();

    private static readonly byte[] RowV1 = { 0x01, 0x02, 0x03, 0x04 };

    private UpdateSupplierCommandHandler CreateHandler(IAccountRepository? accounts = null) =>
        new(_suppliers, _groups, _companies, accounts);

    private static Supplier ExistingSupplier(byte[]? rowVersion = null) => new()
    {
        Id = Guid.NewGuid(),
        Code = "SUP-001",
        Name = "Original Name",
        TaxId = "20-11111111-9",
        PaymentTermsDays = 30,
        IsActive = true,
        RowVersion = rowVersion ?? (byte[])RowV1.Clone(),
    };

    private static UpdateSupplierCommand CommandFor(
        Supplier supplier,
        byte[]? rowVersion = null,
        string? code = null,
        string? name = null,
        Guid? supplierGroupId = null,
        bool isFrozen = false,
        bool onHold = false,
        SupplierHoldType holdType = SupplierHoldType.None,
        DateOnly? releaseDate = null,
        IReadOnlyList<SupplierCompanyAccountInput>? companyAccounts = null) =>
        new(
            supplier.Id,
            code ?? supplier.Code,
            name ?? "Updated Name",
            supplier.TaxId,
            supplier.PaymentTermsDays,
            supplier.DefaultPayableAccountId,
            IsActive: true,
            rowVersion ?? (byte[])RowV1.Clone(),
            SupplierGroupId: supplierGroupId,
            IsFrozen: isFrozen,
            OnHold: onHold,
            HoldType: holdType,
            ReleaseDate: releaseDate,
            CompanyAccounts: companyAccounts);

    // ------------------------------------------------------- SUP-G4: optimistic concurrency

    [Fact]
    public async Task Update_StaleRowVersion_FailsWithConcurrencyConflict()
    {
        var supplier = ExistingSupplier();
        _suppliers.Seed(supplier);

        var result = await CreateHandler().HandleAsync(
            CommandFor(supplier, rowVersion: new byte[] { 9, 9, 9, 9 }, name: "Should Not Apply"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ConcurrencyErrorCodes.ConcurrencyConflict, result.Error!.Code);
        Assert.Equal("Original Name", supplier.Name); // never mutated
    }

    [Fact]
    public async Task Update_MatchingRowVersion_PersistsChanges()
    {
        var supplier = ExistingSupplier();
        _suppliers.Seed(supplier);

        var result = await CreateHandler().HandleAsync(
            CommandFor(supplier, name: "Updated Name", isFrozen: true, onHold: true,
                holdType: SupplierHoldType.Payments, releaseDate: new DateOnly(2026, 12, 31)));

        Assert.True(result.IsSuccess);
        Assert.Equal("Updated Name", supplier.Name);
        Assert.True(supplier.IsFrozen);
        Assert.True(supplier.OnHold);
        Assert.Equal(SupplierHoldType.Payments, supplier.HoldType);
        Assert.Equal(new DateOnly(2026, 12, 31), supplier.ReleaseDate);
    }

    [Fact]
    public async Task Update_UnknownSupplier_FailsWithSupplierNotFound()
    {
        var ghost = ExistingSupplier();

        var result = await CreateHandler().HandleAsync(CommandFor(ghost));

        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseErrorCodes.SupplierNotFound, result.Error!.Code);
    }

    // ------------------------------------------------------- validation regressions

    [Fact]
    public async Task Update_ChangedCodeToExisting_FailsWithDuplicateSupplierCode()
    {
        var supplier = ExistingSupplier();
        var other = ExistingSupplier();
        other.Code = "SUP-002";
        _suppliers.Seed(supplier, other);

        var result = await CreateHandler().HandleAsync(
            CommandFor(supplier, code: "SUP-002"));

        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseErrorCodes.DuplicateSupplierCode, result.Error!.Code);
    }

    [Fact]
    public async Task Update_UnknownGroup_FailsWithSupplierGroupNotFound()
    {
        var supplier = ExistingSupplier();
        _suppliers.Seed(supplier);

        var result = await CreateHandler().HandleAsync(
            CommandFor(supplier, supplierGroupId: Guid.NewGuid()));

        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseErrorCodes.SupplierGroupNotFound, result.Error!.Code);
    }

    [Fact]
    public async Task Update_DuplicateCompanyInAccounts_FailsWithInvalidGlAccount()
    {
        var supplier = ExistingSupplier();
        _suppliers.Seed(supplier);
        var duplicate = Guid.NewGuid();
        _companies.Company = new Company { Id = duplicate, Name = "COMP-001" };

        var result = await CreateHandler().HandleAsync(
            CommandFor(supplier, companyAccounts: new[]
            {
                new SupplierCompanyAccountInput(duplicate, Guid.NewGuid()),
                new SupplierCompanyAccountInput(duplicate, Guid.NewGuid()),
            }));

        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseErrorCodes.InvalidGlAccount, result.Error!.Code);
    }

    // ------------------------------------------------------- full-map replacement

    [Fact]
    public async Task Update_CompanyAccounts_ReplacesTheWholeMap()
    {
        var supplier = ExistingSupplier();
        var staleCompany = Guid.NewGuid();
        supplier.SetCompanyAccount(staleCompany, Guid.NewGuid());
        _suppliers.Seed(supplier);

        var companyA = Guid.NewGuid();
        var companyB = Guid.NewGuid();
        var payableA = Guid.NewGuid();
        var payableB = Guid.NewGuid();
        _companies.Company = new Company { Id = companyA, Name = "COMP-001" };
        _accounts.Seed(new Account { Id = payableA }, new Account { Id = payableB });

        var result = await CreateHandler(_accounts).HandleAsync(
            CommandFor(supplier, companyAccounts: new[]
            {
                new SupplierCompanyAccountInput(companyA, payableA),
                new SupplierCompanyAccountInput(companyB, payableB),
            }));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, supplier.Accounts.Count);
        Assert.DoesNotContain(supplier.Accounts, a => a.CompanyId == staleCompany);
        Assert.Contains(supplier.Accounts, a => a.CompanyId == companyA && a.PayableAccountId == payableA);
        Assert.Contains(supplier.Accounts, a => a.CompanyId == companyB && a.PayableAccountId == payableB);
    }

    // ------------------------------------------------------- SetSupplierHoldStatus (T2.1)

    [Fact]
    public async Task SetHold_OnUnknownSupplier_FailsWithSupplierNotFound()
    {
        var result = await new SetSupplierHoldStatusCommandHandler(_suppliers).HandleAsync(
            new SetSupplierHoldStatusCommand(Guid.NewGuid(), OnHold: true, HoldType: SupplierHoldType.All, ReleaseDate: null));

        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseErrorCodes.SupplierNotFound, result.Error!.Code);
    }

    [Fact]
    public async Task SetHold_WithStaleRowVersion_FailsWithConcurrencyConflict()
    {
        var supplier = ExistingSupplier();
        _suppliers.Seed(supplier);

        var result = await new SetSupplierHoldStatusCommandHandler(_suppliers).HandleAsync(
            new SetSupplierHoldStatusCommand(
                supplier.Id, OnHold: true, HoldType: SupplierHoldType.Invoices, ReleaseDate: null,
                RowVersion: new byte[] { 9, 9, 9, 9 }));

        Assert.False(result.IsSuccess);
        Assert.Equal(ConcurrencyErrorCodes.ConcurrencyConflict, result.Error!.Code);
        Assert.False(supplier.OnHold);
    }

    [Fact]
    public async Task SetHold_WithoutRowVersion_AppliesToCurrentRow()
    {
        var supplier = ExistingSupplier();
        _suppliers.Seed(supplier);

        var result = await new SetSupplierHoldStatusCommandHandler(_suppliers).HandleAsync(
            new SetSupplierHoldStatusCommand(
                supplier.Id, OnHold: true, HoldType: SupplierHoldType.Payments, ReleaseDate: new DateOnly(2026, 12, 31)));

        Assert.True(result.IsSuccess);
        Assert.True(supplier.OnHold);
        Assert.Equal(SupplierHoldType.Payments, supplier.HoldType);
        Assert.Equal(new DateOnly(2026, 12, 31), supplier.ReleaseDate);
    }

    [Fact]
    public async Task SetHold_Releasing_ClearsTypeAndDate()
    {
        var supplier = ExistingSupplier();
        supplier.SetHoldStatus(true, SupplierHoldType.All, new DateOnly(2026, 12, 31));
        _suppliers.Seed(supplier);

        var result = await new SetSupplierHoldStatusCommandHandler(_suppliers).HandleAsync(
            new SetSupplierHoldStatusCommand(supplier.Id, OnHold: false, HoldType: SupplierHoldType.None, ReleaseDate: null));

        Assert.True(result.IsSuccess);
        Assert.False(supplier.OnHold);
        Assert.Equal(SupplierHoldType.None, supplier.HoldType);
        Assert.Null(supplier.ReleaseDate);
    }
}
