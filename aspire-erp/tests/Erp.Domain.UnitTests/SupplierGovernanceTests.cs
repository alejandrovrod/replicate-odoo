using Erp.Domain.Entities;
using Xunit;

namespace Erp.Domain.UnitTests;

/// <summary>
/// Module buying-suppliers, spec §3 (tasks.md T1.3): the governance boundary matrix of
/// <see cref="Supplier.IsBlockedFor"/> and <see cref="Supplier.SetHoldStatus"/> (scenarios SUP-G2 /
/// SUP-G3), plus the per-company account aggregate rule (gap D2).
/// </summary>
public sealed class SupplierGovernanceTests
{
    private static readonly DateOnly Today = new(2026, 10, 15);

    private static Supplier SupplierWith(
        bool isActive = true,
        bool onHold = false,
        SupplierHoldType holdType = SupplierHoldType.None,
        DateOnly? releaseDate = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Code = "SUP-2026-001",
            Name = "Tech Supplies SA",
            IsActive = isActive,
            OnHold = onHold,
            HoldType = holdType,
            ReleaseDate = releaseDate,
        };

    [Fact]
    public void IsBlockedFor_InactiveSupplier_IsBlockedEvenWithoutHold()
    {
        var supplier = SupplierWith(isActive: false);

        Assert.True(supplier.IsBlockedFor(SupplierHoldType.Invoices, Today));
        Assert.True(supplier.IsBlockedFor(SupplierHoldType.Payments, Today));
        Assert.True(supplier.IsBlockedFor(SupplierHoldType.All, Today));
    }

    [Fact]
    public void IsBlockedFor_NoHold_IsNotBlocked()
    {
        var supplier = SupplierWith(onHold: false);

        Assert.False(supplier.IsBlockedFor(SupplierHoldType.Invoices, Today));
        Assert.False(supplier.IsBlockedFor(SupplierHoldType.All, Today));
    }

    [Fact]
    public void IsBlockedFor_HoldAll_BlocksEveryAction()
    {
        var supplier = SupplierWith(onHold: true, holdType: SupplierHoldType.All);

        Assert.True(supplier.IsBlockedFor(SupplierHoldType.Invoices, Today));
        Assert.True(supplier.IsBlockedFor(SupplierHoldType.Payments, Today));
        Assert.True(supplier.IsBlockedFor(SupplierHoldType.All, Today));
    }

    [Fact]
    public void IsBlockedFor_ScopedHold_BlocksOnlyItsTarget()
    {
        // A hold scoped to invoices must NOT block payments, and must not block purchase orders
        // (the order guard asks for HoldType.All).
        var supplier = SupplierWith(onHold: true, holdType: SupplierHoldType.Invoices);

        Assert.True(supplier.IsBlockedFor(SupplierHoldType.Invoices, Today));
        Assert.False(supplier.IsBlockedFor(SupplierHoldType.Payments, Today));
        Assert.False(supplier.IsBlockedFor(SupplierHoldType.All, Today));
    }

    [Fact]
    public void IsBlockedFor_FutureReleaseDate_StillBlocks()
    {
        // SUP-G2: hold Invoices with release 2026-12-31, attempt to bill on 2026-10-15.
        var supplier = SupplierWith(onHold: true, holdType: SupplierHoldType.Invoices, releaseDate: new DateOnly(2026, 12, 31));

        Assert.True(supplier.IsBlockedFor(SupplierHoldType.Invoices, Today));
    }

    [Fact]
    public void IsBlockedFor_ElapsedReleaseDate_Allows()
    {
        // SUP-G3: hold Invoices with release 2026-10-01, bill on 2026-10-15 (strictly past).
        var supplier = SupplierWith(onHold: true, holdType: SupplierHoldType.Invoices, releaseDate: new DateOnly(2026, 10, 1));

        Assert.False(supplier.IsBlockedFor(SupplierHoldType.Invoices, Today));
    }

    [Fact]
    public void IsBlockedFor_ReleaseDateEqualToTheDay_StillBlocks()
    {
        // The release only takes effect AFTER the date, so the date itself is still blocked.
        var supplier = SupplierWith(onHold: true, holdType: SupplierHoldType.Invoices, releaseDate: Today);

        Assert.True(supplier.IsBlockedFor(SupplierHoldType.Invoices, Today));
    }

    [Fact]
    public void SetHoldStatus_ApplyingHold_SetsTypeAndReleaseDate()
    {
        var supplier = SupplierWith();
        var release = new DateOnly(2026, 12, 31);

        supplier.SetHoldStatus(onHold: true, holdType: SupplierHoldType.Payments, releaseDate: release);

        Assert.True(supplier.OnHold);
        Assert.Equal(SupplierHoldType.Payments, supplier.HoldType);
        Assert.Equal(release, supplier.ReleaseDate);
    }

    [Fact]
    public void SetHoldStatus_Releasing_ClearsTypeAndReleaseDate()
    {
        var supplier = SupplierWith(onHold: true, holdType: SupplierHoldType.All, releaseDate: new DateOnly(2026, 12, 31));

        supplier.SetHoldStatus(onHold: false, holdType: SupplierHoldType.None);

        Assert.False(supplier.OnHold);
        Assert.Equal(SupplierHoldType.None, supplier.HoldType);
        Assert.Null(supplier.ReleaseDate);
    }

    [Fact]
    public void SetCompanyAccount_AddsOneRowPerCompany()
    {
        var supplier = SupplierWith();
        var companyA = Guid.NewGuid();
        var companyB = Guid.NewGuid();
        var payableA = Guid.NewGuid();
        var payableB = Guid.NewGuid();

        supplier.SetCompanyAccount(companyA, payableA);
        supplier.SetCompanyAccount(companyB, payableB);

        Assert.Equal(2, supplier.Accounts.Count);
        Assert.All(supplier.Accounts, account => Assert.Equal(supplier.Id, account.SupplierId));
    }

    [Fact]
    public void SetCompanyAccount_SameCompanyTwice_UpdatesInPlace()
    {
        var supplier = SupplierWith();
        var company = Guid.NewGuid();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var advance = Guid.NewGuid();

        supplier.SetCompanyAccount(company, first);
        var originalId = Assert.Single(supplier.Accounts).Id;

        supplier.SetCompanyAccount(company, second, advance);

        var account = Assert.Single(supplier.Accounts);
        Assert.Equal(originalId, account.Id); // same row, not a duplicate
        Assert.Equal(second, account.PayableAccountId);
        Assert.Equal(advance, account.AdvanceAccountId);
    }
}
