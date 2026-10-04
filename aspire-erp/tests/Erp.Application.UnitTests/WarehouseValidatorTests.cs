using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Task 3.1 acceptance ("Warehouses enforce tree structure") as unit tests of the pure Domain
/// validator: field rules, parent rules and cycle prevention, mirroring the Account tree.
/// </summary>
public sealed class WarehouseValidatorTests
{
    private static readonly Guid CompanyA = Guid.NewGuid();
    private static readonly Guid CompanyB = Guid.NewGuid();
    private static readonly Guid StockAccount = Guid.NewGuid();

    private static Warehouse NewWarehouse(Guid id, Guid companyId, bool isGroup = false, Guid? parentId = null) =>
        new()
        {
            Id = id,
            CompanyId = companyId,
            WarehouseCode = "WH-1",
            WarehouseName = "Warehouse",
            AccountId = StockAccount,
            IsGroup = isGroup,
            ParentWarehouseId = parentId,
        };

    // ---------------------------------------------------------------------- field rules

    [Fact]
    public void EnsureValidFields_ValidWarehouse_DoesNotThrow()
    {
        WarehouseValidator.EnsureValidFields(CompanyA, "SN", "Stores - North", StockAccount);
    }

    [Theory]
    [InlineData("warehouse_company_required")]
    public void EnsureValidFields_MissingCompany_Throws(string expectedCode)
    {
        var ex = Assert.Throws<StockValidationException>(
            () => WarehouseValidator.EnsureValidFields(Guid.Empty, "SN", "Stores", StockAccount));

        Assert.Equal(expectedCode, ex.Code);
    }

    [Fact]
    public void EnsureValidFields_MissingStockAccount_Throws()
    {
        var ex = Assert.Throws<StockValidationException>(
            () => WarehouseValidator.EnsureValidFields(CompanyA, "SN", "Stores", Guid.Empty));

        Assert.Equal(StockErrorCodes.MissingStockAccount, ex.Code);
    }

    [Fact]
    public void EnsureValidFields_BlankCode_Throws()
    {
        var ex = Assert.Throws<StockValidationException>(
            () => WarehouseValidator.EnsureValidFields(CompanyA, "   ", "Stores", StockAccount));

        Assert.Equal(StockErrorCodes.WarehouseCodeRequired, ex.Code);
    }

    [Fact]
    public void EnsureValidFields_TooLongCode_Throws()
    {
        var ex = Assert.Throws<StockValidationException>(
            () => WarehouseValidator.EnsureValidFields(CompanyA, new string('x', 51), "Stores", StockAccount));

        Assert.Equal(StockErrorCodes.WarehouseCodeTooLong, ex.Code);
    }

    [Fact]
    public void EnsureValidFields_TooLongName_Throws()
    {
        var ex = Assert.Throws<StockValidationException>(
            () => WarehouseValidator.EnsureValidFields(CompanyA, "SN", new string('x', 151), StockAccount));

        Assert.Equal(StockErrorCodes.WarehouseNameTooLong, ex.Code);
    }

    // ----------------------------------------------------------------------- parent rules

    [Fact]
    public void EnsureValidParent_GroupParentOfSameCompany_DoesNotThrow()
    {
        var parent = NewWarehouse(Guid.NewGuid(), CompanyA, isGroup: true);
        var child = NewWarehouse(Guid.NewGuid(), CompanyA, parentId: parent.Id);

        WarehouseValidator.EnsureValidParent(child, parent);
    }

    [Fact]
    public void EnsureValidParent_SelfReference_Throws()
    {
        var candidate = NewWarehouse(Guid.NewGuid(), CompanyA, parentId: null);
        candidate.ParentWarehouseId = candidate.Id;

        var ex = Assert.Throws<StockValidationException>(
            () => WarehouseValidator.EnsureValidParent(candidate, candidate));

        Assert.Equal(StockErrorCodes.ParentIsSelf, ex.Code);
    }

    [Fact]
    public void EnsureValidParent_ParentFromAnotherCompany_Throws()
    {
        var parent = NewWarehouse(Guid.NewGuid(), CompanyB, isGroup: true);
        var child = NewWarehouse(Guid.NewGuid(), CompanyA, parentId: parent.Id);

        var ex = Assert.Throws<StockValidationException>(
            () => WarehouseValidator.EnsureValidParent(child, parent));

        Assert.Equal(StockErrorCodes.ParentNotInSameCompany, ex.Code);
    }

    [Fact]
    public void EnsureValidParent_NonGroupParent_Throws()
    {
        var parent = NewWarehouse(Guid.NewGuid(), CompanyA, isGroup: false);
        var child = NewWarehouse(Guid.NewGuid(), CompanyA, parentId: parent.Id);

        var ex = Assert.Throws<StockValidationException>(
            () => WarehouseValidator.EnsureValidParent(child, parent));

        Assert.Equal(StockErrorCodes.ParentIsNotGroup, ex.Code);
    }

    // ---------------------------------------------------------------------- cycle rules

    [Fact]
    public void EnsureNoCycle_CandidateInsideAncestorChain_Throws()
    {
        var candidate = Guid.NewGuid();
        var ancestorChain = new List<Guid> { candidate, Guid.NewGuid() };

        var ex = Assert.Throws<StockValidationException>(
            () => WarehouseValidator.EnsureNoCycle(candidate, ancestorChain));

        Assert.Equal(StockErrorCodes.CycleDetected, ex.Code);
    }

    [Fact]
    public void EnsureNoCycle_LoopingStoredChain_Throws()
    {
        var candidate = Guid.NewGuid();
        var loopId = Guid.NewGuid();
        var ancestorChain = new List<Guid> { loopId, Guid.NewGuid(), loopId };

        var ex = Assert.Throws<StockValidationException>(
            () => WarehouseValidator.EnsureNoCycle(candidate, ancestorChain));

        Assert.Equal(StockErrorCodes.CycleDetected, ex.Code);
    }

    [Fact]
    public void EnsureNoCycle_LinearChain_DoesNotThrow()
    {
        var candidate = Guid.NewGuid();
        var ancestorChain = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        WarehouseValidator.EnsureNoCycle(candidate, ancestorChain); // must not throw
    }

    [Fact]
    public void EnsureNoCycle_EmptyChain_DoesNotThrow()
    {
        WarehouseValidator.EnsureNoCycle(Guid.NewGuid(), Array.Empty<Guid>()); // must not throw
    }

    [Fact]
    public void EnsureNoCycle_NullChain_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => WarehouseValidator.EnsureNoCycle(Guid.NewGuid(), null!));
    }
}

