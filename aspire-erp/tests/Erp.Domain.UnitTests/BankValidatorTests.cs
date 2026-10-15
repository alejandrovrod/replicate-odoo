using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Xunit;

namespace Erp.Domain.UnitTests;

/// <summary>
/// Pure-domain validation of the offline <see cref="Bank"/> catalog master (bank-catalog
/// spec §3): required fields, ISO 3166-1 alpha-2 country (uppercase), MX 3-digit clave.
/// Zero I/O per Constitution Article I.2.
/// </summary>
public sealed class BankValidatorTests
{
    [Fact]
    public void EnsureValidFields_ValidMxBank_DoesNotThrow()
    {
        BankValidator.EnsureValidFields("MX", "012", "BBVA México", "BCMRMXMM");
    }

    [Fact]
    public void NormalizeCountryCode_LowercaseAndSpaces_NormalizesToUpper()
    {
        Assert.Equal("MX", BankValidator.NormalizeCountryCode("  mx "));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("M")]
    [InlineData("MXX")]
    [InlineData("12")]
    [InlineData("XX")]
    public void NormalizeCountryCode_Invalid_ThrowsInvalidBankCountry(string? country)
    {
        var ex = Assert.Throws<BankingValidationException>(
            () => BankValidator.NormalizeCountryCode(country));
        Assert.Equal(BankingErrorCodes.InvalidBankCountry, ex.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EnsureValidFields_EmptyCode_Throws(string? code)
    {
        var ex = Assert.Throws<BankingValidationException>(
            () => BankValidator.EnsureValidFields("MX", code, "BBVA México", null));
        Assert.Equal(BankingErrorCodes.InvalidBankCode, ex.Code);
    }

    [Theory]
    [InlineData("12")]
    [InlineData("0123")]
    [InlineData("ABC")]
    public void EnsureValidFields_NonThreeDigitMxCode_Throws(string code)
    {
        var ex = Assert.Throws<BankingValidationException>(
            () => BankValidator.EnsureValidFields("MX", code, "Banco X", null));
        Assert.Equal(BankingErrorCodes.InvalidBankCode, ex.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EnsureValidFields_EmptyName_ThrowsBankNameRequired(string? name)
    {
        var ex = Assert.Throws<BankingValidationException>(
            () => BankValidator.EnsureValidFields("MX", "012", name, null));
        Assert.Equal(BankingErrorCodes.BankNameRequired, ex.Code);
    }

    [Fact]
    public void EnsureValidFields_TooLongSwiftBic_Throws()
    {
        var ex = Assert.Throws<BankingValidationException>(
            () => BankValidator.EnsureValidFields("MX", "012", "BBVA México", "TOOLONG123456"));
        Assert.Equal(BankingErrorCodes.InvalidBankCode, ex.Code);
    }
}
