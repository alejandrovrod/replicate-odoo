using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Xunit;

namespace Erp.Domain.UnitTests;

/// <summary>
/// Tenant subdomain invariants (multi-tenant onboarding spec): DNS-label shape, reserved names,
/// normalization and host extraction. Pure Domain, no database.
/// </summary>
public sealed class TenantSubdomainTests
{
    [Theory]
    [InlineData("acme")]
    [InlineData("acme-corp")]
    [InlineData("a1b")]
    [InlineData("tenant-01")]
    [InlineData("a1234567890123456789012345678901")] // 32 chars (maximum)
    public void IsValid_AcceptsLegalLabels(string value)
    {
        Assert.True(TenantSubdomain.IsValid(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ab")] // too short
    [InlineData("-acme")]
    [InlineData("acme-")]
    [InlineData("ac_me")]
    [InlineData("ac.me")]
    [InlineData("a12345678901234567890123456789012")] // 33 chars (too long)
    public void IsValid_RejectsMalformedLabels(string? value)
    {
        Assert.False(TenantSubdomain.IsValid(value));
        Assert.False(TenantSubdomain.IsFormatValid(value));
    }

    [Theory]
    [InlineData("api")]
    [InlineData("app")]
    [InlineData("admin")]
    [InlineData("auth")]
    [InlineData("login")]
    [InlineData("signup")]
    [InlineData("register")]
    [InlineData("www")]
    [InlineData("mail")]
    [InlineData("status")]
    [InlineData("dev")]
    [InlineData("staging")]
    [InlineData("ADMIN")]
    public void IsValid_RejectsReservedNames(string value)
    {
        Assert.True(TenantSubdomain.IsFormatValid(value));
        Assert.True(TenantSubdomain.IsReserved(value));
        Assert.False(TenantSubdomain.IsValid(value));
    }

    [Fact]
    public void EnsureValid_NormalizesTrimAndCase()
    {
        Assert.Equal("acme", TenantSubdomain.EnsureValid("  ACME  "));
    }

    [Fact]
    public void EnsureValid_InvalidShape_ThrowsSubdomainInvalid()
    {
        var exception = Assert.Throws<AuthValidationException>(
            () => TenantSubdomain.EnsureValid("-bad-"));

        Assert.Equal(TenantErrorCodes.SubdomainInvalid, exception.Code);
    }

    [Fact]
    public void EnsureValid_ReservedName_ThrowsSubdomainReserved()
    {
        var exception = Assert.Throws<AuthValidationException>(
            () => TenantSubdomain.EnsureValid("Admin"));

        Assert.Equal(TenantErrorCodes.SubdomainReserved, exception.Code);
    }

    [Theory]
    [InlineData("acme.domain.com", "acme")]
    [InlineData("ACME.Domain.COM", "acme")]
    [InlineData("acme.domain.com:443", "acme")]
    [InlineData("acme.domain.com.", "acme")]
    [InlineData("acme.localhost", "acme")]
    [InlineData("acme.localhost:5173", "acme")]
    [InlineData("erp.example.org", "erp", "example.org")]
    public void ExtractFromHost_ReturnsTenantLabel(
        string host,
        string expected,
        string? baseDomain = null)
    {
        Assert.Equal(expected, TenantSubdomain.ExtractFromHost(host, baseDomain));
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("domain.com")]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    [InlineData("api.domain.com")] // reserved
    [InlineData("a.b.domain.com")] // nested label
    [InlineData("unrelated.example.com")]
    [InlineData("")]
    [InlineData(null)]
    public void ExtractFromHost_ReturnsNullWhenNoTenantLabel(string? host)
    {
        Assert.Null(TenantSubdomain.ExtractFromHost(host));
    }
}
