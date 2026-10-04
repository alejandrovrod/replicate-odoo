using Erp.Application.DTOs;
using Erp.Application.Features.Items.Commands;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Task 3.1 acceptance ("Items validate unique SKU per tenant") exercised through the CQRS handler:
/// pure validation, FK existence checks and the duplicate-SKU rule surfacing as a domain failure.
/// </summary>
public sealed class CreateItemCommandHandlerTests
{
    private readonly FakeItemRepository _items = new();
    private readonly FakeUomRepository _uoms = new();
    private readonly FakeAccountRepository _accounts = new();

    private readonly Guid _uomId = Guid.NewGuid();
    private readonly Guid _expenseAccountId = Guid.NewGuid();

    public CreateItemCommandHandlerTests()
    {
        _uoms.Seed(new UOM { Id = _uomId, UomName = "Each", Symbol = "EA", MustBeWholeNumber = true });
        _accounts.Seed(new Account
        {
            Id = _expenseAccountId,
            AccountCode = "5210",
            AccountName = "Cost of Goods Sold",
            IsActive = true,
            IsGroup = false,
        });
    }

    private CreateItemCommandHandler CreateHandler() => new(_items, _uoms, _accounts);

    [Fact]
    public async Task HandleAsync_ValidItem_PersistsTrimmedItemAndReturnsDto()
    {
        var command = new CreateItemCommand(
            Code: "  IT-001  ",
            Name: " Steel Bracket ",
            ValuationMethod: ValuationMethod.Fifo,
            BaseUOMId: _uomId,
            ExpenseAccountId: _expenseAccountId);

        var result = await CreateHandler().HandleAsync(command);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("IT-001", result.Value!.Code); // trimmed
        Assert.Equal("Steel Bracket", result.Value.Name);
        Assert.Equal(_uomId, result.Value.BaseUOMId);

        var saved = _items.AddedItem;
        Assert.NotNull(saved);
        Assert.Equal("IT-001", saved!.ItemCode);
        Assert.Equal(Guid.Empty, saved.TenantId); // stamped by AppDbContext, never by the handler
    }

    [Fact]
    public async Task HandleAsync_DuplicateSkuInTenant_FailsWithDuplicateItemCode()
    {
        _items.SkuExists = true;

        var result = await CreateHandler().HandleAsync(
            new CreateItemCommand("IT-001", "Bracket", ValuationMethod.Fifo, _uomId));

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("duplicate_item_code", result.Error!.Code);
        Assert.Null(_items.AddedItem);
    }

    [Fact]
    public async Task HandleAsync_MissingBaseUom_Fails()
    {
        var result = await CreateHandler().HandleAsync(
            new CreateItemCommand("IT-001", "Bracket", ValuationMethod.Fifo, Guid.NewGuid()));

        Assert.False(result.IsSuccess);
        Assert.Equal("base_uom_required", result.Error!.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleAsync_BlankCode_Fails(string code)
    {
        var result = await CreateHandler().HandleAsync(
            new CreateItemCommand(code, "Bracket", ValuationMethod.Fifo, _uomId));

        Assert.False(result.IsSuccess);
        Assert.Equal("item_code_required", result.Error!.Code);
    }

    [Fact]
    public async Task HandleAsync_UnknownExpenseAccount_Fails()
    {
        var result = await CreateHandler().HandleAsync(
            new CreateItemCommand(
                "IT-001", "Bracket", ValuationMethod.Fifo, _uomId,
                ExpenseAccountId: Guid.NewGuid()));

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_gl_account", result.Error!.Code);
    }

    [Fact]
    public async Task HandleAsync_UndefinedValuationMethod_Fails()
    {
        var result = await CreateHandler().HandleAsync(
            new CreateItemCommand("IT-001", "Bracket", (ValuationMethod)42, _uomId));

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_valuation_method", result.Error!.Code);
    }
}

