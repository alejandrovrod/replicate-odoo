using Erp.Application.Features.Currencies.Commands;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// RM-09 acceptance exercised through the CQRS handlers: pure Domain validation
/// (CurrencyValidator), the duplicate-code rule and the RowVersion compare-and-swap
/// on update (mirrors CreateSupplierCommandHandlerTests).
/// </summary>
public sealed class CreateCurrencyCommandHandlerTests
{
    private readonly FakeCurrencyRepository _currencies = new();

    [Fact]
    public async Task HandleAsync_ValidCurrency_PersistsNormalizedCodeAndReturnsDto()
    {
        var result = await new CreateCurrencyCommandHandler(_currencies).HandleAsync(
            new CreateCurrencyCommand("  usd  ", " $ ", "Cent "));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("USD", result.Value!.Code); // trimmed + uppercased
        Assert.Equal("$", result.Value.Symbol);
        Assert.Equal("Cent", result.Value.FractionName);
        Assert.True(result.Value.IsActive);

        var saved = _currencies.AddedCurrency;
        Assert.NotNull(saved);
        Assert.Equal("USD", saved!.Code);
    }

    [Fact]
    public async Task HandleAsync_DuplicateCode_FailsWithDuplicateCurrencyCode()
    {
        _currencies.CodeExists = true;

        var result = await new CreateCurrencyCommandHandler(_currencies).HandleAsync(
            new CreateCurrencyCommand("USD", "$"));

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(CurrencyErrorCodes.DuplicateCurrencyCode, result.Error!.Code);
        Assert.Null(_currencies.AddedCurrency);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleAsync_BlankCode_Fails(string code)
    {
        var result = await new CreateCurrencyCommandHandler(_currencies).HandleAsync(
            new CreateCurrencyCommand(code, "$"));

        Assert.False(result.IsSuccess);
        Assert.Equal(CurrencyErrorCodes.CodeRequired, result.Error!.Code);
    }

    [Fact]
    public async Task HandleAsync_CodeOver3Chars_Fails()
    {
        var result = await new CreateCurrencyCommandHandler(_currencies).HandleAsync(
            new CreateCurrencyCommand("USDX", "$"));

        Assert.False(result.IsSuccess);
        Assert.Equal(CurrencyErrorCodes.CodeTooLong, result.Error!.Code);
    }

    [Fact]
    public async Task UpdateAsync_StaleRowVersion_FailsWithConcurrencyConflict()
    {
        var currency = new Currency
        {
            Id = Guid.NewGuid(),
            Code = "USD",
            Symbol = "$",
            FractionName = "Cent",
            RowVersion = new byte[] { 1, 2, 3, 4 },
        };
        _currencies.Seed(currency);

        var result = await new UpdateCurrencyCommandHandler(_currencies).HandleAsync(
            new UpdateCurrencyCommand(currency.Id, "USD", "$", "Cent", true, new byte[] { 9, 9, 9, 9 }));

        Assert.False(result.IsSuccess);
        Assert.Equal("concurrency_conflict", result.Error!.Code);
    }

    [Fact]
    public async Task UpdateAsync_MatchingRowVersion_PersistsChanges()
    {
        var rowVersion = new byte[] { 1, 2, 3, 4 };
        var currency = new Currency
        {
            Id = Guid.NewGuid(),
            Code = "USD",
            Symbol = "$",
            FractionName = "Cent",
            RowVersion = rowVersion,
        };
        _currencies.Seed(currency);

        var result = await new UpdateCurrencyCommandHandler(_currencies).HandleAsync(
            new UpdateCurrencyCommand(currency.Id, "USD", "US$", "Cent", false, rowVersion));

        Assert.True(result.IsSuccess);
        Assert.Equal("US$", result.Value!.Symbol);
        Assert.False(result.Value.IsActive);
    }
}
