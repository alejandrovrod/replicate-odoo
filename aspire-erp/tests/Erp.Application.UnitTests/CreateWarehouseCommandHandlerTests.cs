using Erp.Application.DTOs;
using Erp.Application.Features.Warehouses.Commands;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Task 3.1 acceptance ("Warehouses enforce tree structure") through the CQRS handler: parent
/// resolution, cycle prevention and the duplicate-code-per-company rule.
/// </summary>
public sealed class CreateWarehouseCommandHandlerTests
{
    private readonly FakeWarehouseRepository _warehouses = new();
    private readonly FakeAccountRepository _accounts = new();

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _stockAccountId = Guid.NewGuid();
    private readonly Guid _parentWarehouseId = Guid.NewGuid();

    public CreateWarehouseCommandHandlerTests()
    {
        _accounts.Seed(new Account
        {
            Id = _stockAccountId,
            AccountCode = "1310",
            AccountName = "Stock In Hand",
            IsActive = true,
            IsGroup = false,
        });
    }

    private CreateWarehouseCommandHandler CreateHandler() => new(_warehouses, _accounts);

    private Warehouse GroupWarehouse(Guid id, Guid companyId) =>
        new()
        {
            Id = id,
            CompanyId = companyId,
            WarehouseCode = "GRP",
            WarehouseName = "Group",
            IsGroup = true,
            AccountId = _stockAccountId,
        };

    [Fact]
    public async Task HandleAsync_RootWarehouse_PersistsAndReturnsDto()
    {
        var command = new CreateWarehouseCommand(_companyId, " SN ", "Stores - North", _stockAccountId);

        var result = await CreateHandler().HandleAsync(command);

        Assert.True(result.IsSuccess);
        Assert.Equal("SN", result.Value!.Code); // trimmed
        var saved = _warehouses.AddedWarehouse;
        Assert.NotNull(saved);
        Assert.Equal("SN", saved!.Code);
        Assert.Null(saved.ParentWarehouseId);
        Assert.Equal(Guid.Empty, saved.TenantId); // stamped by AppDbContext
    }

    [Fact]
    public async Task HandleAsync_ChildOfGroupWarehouse_Succeeds()
    {
        _warehouses.Ancestors = new[] { GroupWarehouse(_parentWarehouseId, _companyId) };

        var command = new CreateWarehouseCommand(
            _companyId, "SN-A1", "Aisle 1", _stockAccountId, ParentWarehouseId: _parentWarehouseId);

        var result = await CreateHandler().HandleAsync(command);

        Assert.True(result.IsSuccess);
        Assert.Equal(_parentWarehouseId, _warehouses.AddedWarehouse!.ParentWarehouseId);
    }

    [Fact]
    public async Task HandleAsync_ParentFromAnotherCompany_Fails()
    {
        _warehouses.Ancestors = new[] { GroupWarehouse(_parentWarehouseId, Guid.NewGuid()) };

        var command = new CreateWarehouseCommand(
            _companyId, "SN-A1", "Aisle 1", _stockAccountId, ParentWarehouseId: _parentWarehouseId);

        var result = await CreateHandler().HandleAsync(command);

        Assert.False(result.IsSuccess);
        Assert.Equal("parent_not_in_same_company", result.Error!.Code);
        Assert.Null(_warehouses.AddedWarehouse);
    }

    [Fact]
    public async Task HandleAsync_LoopingStoredParentChain_FailsWithCycleDetected()
    {
        // The stored chain of the proposed parent already loops back on itself
        // (parent -> grandparent -> parent): the handler must refuse to graft onto it.
        var parentId = Guid.NewGuid();
        var grandparentId = Guid.NewGuid();
        _warehouses.Ancestors = new[]
        {
            GroupWarehouse(parentId, _companyId),
            GroupWarehouse(grandparentId, _companyId),
            GroupWarehouse(parentId, _companyId), // the loop
        };

        var command = new CreateWarehouseCommand(
            _companyId, "SN-A1", "Aisle 1", _stockAccountId, ParentWarehouseId: parentId);

        var result = await CreateHandler().HandleAsync(command);

        Assert.False(result.IsSuccess);
        Assert.Equal("cycle_detected", result.Error!.Code);
        Assert.Null(_warehouses.AddedWarehouse);
    }

    [Fact]
    public async Task HandleAsync_UnknownParent_Fails()
    {
        _warehouses.Ancestors = Array.Empty<Warehouse>();

        var command = new CreateWarehouseCommand(
            _companyId, "SN-A1", "Aisle 1", _stockAccountId, ParentWarehouseId: Guid.NewGuid());

        var result = await CreateHandler().HandleAsync(command);

        Assert.False(result.IsSuccess);
        Assert.Equal("parent_warehouse_not_found", result.Error!.Code);
    }

    [Fact]
    public async Task HandleAsync_DuplicateCodeInCompany_FailsWithDuplicateWarehouseCode()
    {
        // Seed an existing warehouse with the same code in the same company.
        _warehouses.Seed(new Warehouse
        {
            Id = Guid.NewGuid(),
            CompanyId = _companyId,
            WarehouseCode = "SN",
            WarehouseName = "Already there",
            AccountId = _stockAccountId,
        });

        var result = await CreateHandler().HandleAsync(
            new CreateWarehouseCommand(_companyId, "SN", "Stores - North", _stockAccountId));

        Assert.False(result.IsSuccess);
        Assert.Equal("duplicate_warehouse_code", result.Error!.Code);
        Assert.Null(_warehouses.AddedWarehouse);
    }

    [Fact]
    public async Task HandleAsync_MissingStockAccount_Fails()
    {
        var result = await CreateHandler().HandleAsync(
            new CreateWarehouseCommand(_companyId, "SN", "Stores", Guid.Empty));

        Assert.False(result.IsSuccess);
        Assert.Equal("missing_stock_account", result.Error!.Code);
    }

    [Fact]
    public async Task HandleAsync_UnknownLinkedAccount_Fails()
    {
        var result = await CreateHandler().HandleAsync(
            new CreateWarehouseCommand(_companyId, "SN", "Stores", Guid.NewGuid()));

        Assert.False(result.IsSuccess);
        Assert.Equal("missing_stock_account", result.Error!.Code);
    }

    [Fact]
    public async Task HandleAsync_BlankCode_Fails()
    {
        var result = await CreateHandler().HandleAsync(
            new CreateWarehouseCommand(_companyId, "  ", "Stores", _stockAccountId));

        Assert.False(result.IsSuccess);
        Assert.Equal("warehouse_code_required", result.Error!.Code);
    }
}



