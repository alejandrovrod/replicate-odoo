using Erp.Application.Features.Tenants;
using Erp.Application.Services;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using Erp.Infrastructure.Seeders;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Multi-tenant onboarding CQRS: normalization, reserved-name rejection, tenant-scoped
/// subdomain availability and delegation of the atomic write.
/// </summary>
public sealed class TenantOnboardingHandlerTests
{
    private readonly FakeTenantOnboardingRepository _tenants = new();
    private readonly PasswordHasher _passwords = new();

    private RegisterTenantCommandHandler Handler() => new(_tenants, _passwords);

    private static RegisterTenantCommand ValidCommand(
        string subdomain = "acme",
        string password = "TenantAdmin123!",
        string defaultLanguage = "es") =>
        new(
            TenantName: "  Acme Corporation ",
            CompanyName: " Acme HQ ",
            Subdomain: subdomain,
            AdminFullName: "  Ada Lovelace ",
            AdminEmail: "  ADA@Example.COM ",
            AdminPassword: password,
            CompanyTaxId: " TAX-01 ",
            DefaultLanguage: defaultLanguage);

    [Fact]
    public async Task Register_ValidPayload_NormalizesAndHashes()
    {
        var result = await Handler().HandleAsync(ValidCommand("  Acme-Corp  "));

        Assert.True(result.IsSuccess);
        Assert.Equal(_tenants.NextTenantId, result.Value!.TenantId);
        Assert.Equal(_tenants.NextCompanyId, result.Value.CompanyId);
        Assert.Equal(_tenants.NextAdminUserId, result.Value.AdminUserId);
        Assert.Equal("acme-corp", result.Value.Subdomain);

        var data = Assert.IsType<TenantRegistrationData>(_tenants.LastRegistration);
        Assert.Equal("Acme Corporation", data.TenantName);
        Assert.Equal("Acme HQ", data.CompanyName);
        Assert.Equal("ACME-CORP", data.TenantCode);
        Assert.Equal("acme-corp", data.Subdomain);
        Assert.Equal("Ada Lovelace", data.AdminFullName);
        Assert.Equal("ada@example.com", data.AdminEmail);
        Assert.Equal("TAX-01", data.CompanyTaxId);
        Assert.Equal("es", data.DefaultLanguage);
        Assert.DoesNotContain("TenantAdmin123!", data.AdminPasswordHash);
        Assert.True(_passwords.Verify("TenantAdmin123!", data.AdminPasswordHash));
    }

    [Theory]
    [InlineData(
        "en",
        "Assets",
        "Accounts Receivable",
        "Each",
        "EA",
        "All Warehouses",
        "Main Stores",
        "IT Hardware",
        "Unrestricted access (authorization bypass).")]
    [InlineData(
        "es",
        "Activos",
        "Cuentas por Cobrar",
        "Unit",
        "PZA",
        "Todos los Almacenes",
        "Almacén Principal",
        "Equipo de Cómputo y TI",
        "Acceso total sin restricciones al sistema.")]
    public async Task Register_DefaultLanguage_FlowsToLocalizedStarterData(
        string language,
        string assetsName,
        string receivableName,
        string uomName,
        string uomSymbol,
        string rootWarehouseName,
        string leafWarehouseName,
        string assetCategoryName,
        string systemManagerDescription)
    {
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        _tenants.NextTenantId = tenantId;
        _tenants.NextCompanyId = companyId;

        var result = await Handler().HandleAsync(ValidCommand(defaultLanguage: language));

        Assert.True(result.IsSuccess);
        var data = Assert.IsType<TenantRegistrationData>(_tenants.LastRegistration);
        Assert.Equal(language, data.DefaultLanguage);

        var accounts = StandardChartOfAccounts.Build(
            tenantId,
            companyId,
            data.DefaultLanguage);
        var starter = TenantStarterDataSeeder.Build(
            tenantId,
            companyId,
            accounts,
            data.DefaultLanguage);

        Assert.Equal(assetsName, accounts["1000"].AccountName);
        Assert.Equal(receivableName, accounts["1120"].AccountName);
        Assert.Equal(
            accounts["1510"].Id,
            starter.AssetCategories[0].FixedAssetAccountId);
        Assert.Equal(
            accounts["1520"].Id,
            starter.AssetCategories[0].AccumulatedDepreciationAccountId);
        Assert.Equal(
            accounts["5310"].Id,
            starter.AssetCategories[0].DepreciationExpenseAccountId);
        Assert.Equal(
            accounts["2120"].Id,
            starter.AssetCategories[0].CwipAccountId);
        Assert.Equal(
            accounts["4220"].Id,
            starter.AssetCategories[0].GainOnDisposalAccountId);
        Assert.Equal(
            accounts["5320"].Id,
            starter.AssetCategories[0].LossOnDisposalAccountId);
        Assert.Equal(uomName, starter.UnitsOfMeasure[0].UomName);
        Assert.Equal(uomSymbol, starter.UnitsOfMeasure[0].Symbol);
        Assert.Equal(rootWarehouseName, starter.Warehouses[0].WarehouseName);
        Assert.Equal(leafWarehouseName, starter.Warehouses[1].WarehouseName);
        Assert.Equal(assetCategoryName, starter.AssetCategories[0].CategoryName);
        Assert.Equal(
            systemManagerDescription,
            SecurityRoleSeeder.GetDescription("System Manager", data.DefaultLanguage));
    }

    [Theory]
    [InlineData("admin", TenantErrorCodes.SubdomainReserved)]
    [InlineData("www", TenantErrorCodes.SubdomainReserved)]
    [InlineData("ab", TenantErrorCodes.SubdomainInvalid)]
    [InlineData("-acme", TenantErrorCodes.SubdomainInvalid)]
    [InlineData("ac_me", TenantErrorCodes.SubdomainInvalid)]
    public async Task Register_InvalidSubdomain_ReturnsPreciseCode(
        string subdomain,
        string expectedCode)
    {
        var result = await Handler().HandleAsync(ValidCommand(subdomain));

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedCode, result.Error!.Code);
        Assert.Null(_tenants.LastRegistration);
    }

    [Fact]
    public async Task Register_TakenSubdomain_ReturnsConflict()
    {
        _tenants.ExistingSubdomains.Add("acme");

        var result = await Handler().HandleAsync(ValidCommand("ACME"));

        Assert.False(result.IsSuccess);
        Assert.Equal(TenantErrorCodes.SubdomainTaken, result.Error!.Code);
        Assert.Null(_tenants.LastRegistration);
    }

    [Fact]
    public async Task Register_InvalidAdminFields_ReturnsInvalidTenantData()
    {
        var result = await Handler().HandleAsync(new RegisterTenantCommand(
            TenantName: "Acme",
            CompanyName: "Acme HQ",
            Subdomain: "acme",
            AdminFullName: "Admin",
            AdminEmail: "not-an-email",
            AdminPassword: "TenantAdmin123!"));

        Assert.False(result.IsSuccess);
        Assert.Equal(TenantErrorCodes.InvalidTenantData, result.Error!.Code);
        Assert.Null(_tenants.LastRegistration);
    }

    [Fact]
    public async Task Register_WeakPassword_ReturnsInvalidTenantData()
    {
        var result = await Handler().HandleAsync(ValidCommand(password: "short"));

        Assert.False(result.IsSuccess);
        Assert.Equal(TenantErrorCodes.InvalidTenantData, result.Error!.Code);
        Assert.Null(_tenants.LastRegistration);
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("")]
    public async Task Register_UnsupportedDefaultLanguage_ReturnsInvalidTenantData(
        string defaultLanguage)
    {
        var result = await Handler().HandleAsync(
            ValidCommand(defaultLanguage: defaultLanguage));

        Assert.False(result.IsSuccess);
        Assert.Equal(TenantErrorCodes.InvalidTenantData, result.Error!.Code);
        Assert.Null(_tenants.LastRegistration);
    }

    [Fact]
    public async Task Register_RepositoryRace_ReturnsSubdomainTaken()
    {
        _tenants.RegisterException = new AuthValidationException(
            TenantErrorCodes.SubdomainTaken,
            "race");

        var result = await Handler().HandleAsync(ValidCommand());

        Assert.False(result.IsSuccess);
        Assert.Equal(TenantErrorCodes.SubdomainTaken, result.Error!.Code);
    }

    [Theory]
    [InlineData("acme", true)]
    [InlineData("ACME", true)]
    [InlineData("taken", false)]
    [InlineData("admin", false)]
    [InlineData("ab", false)]
    public async Task CheckAvailability_ReturnsExpected(string subdomain, bool expected)
    {
        _tenants.ExistingSubdomains.Add("taken");

        var available = await new CheckSubdomainAvailabilityQueryHandler(_tenants)
            .HandleAsync(new CheckSubdomainAvailabilityQuery(subdomain));

        Assert.Equal(expected, available);
    }

    [Fact]
    public async Task GetBySubdomain_ReturnsLookup()
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Acme",
            Code = "ACME",
            Subdomain = "acme",
            DefaultLanguage = "en",
            IsActive = true,
        };
        _tenants.SeedTenant(tenant);

        var result = await new GetTenantBySubdomainQueryHandler(_tenants)
            .HandleAsync(new GetTenantBySubdomainQuery("  ACME "));

        Assert.True(result.IsSuccess);
        Assert.Equal(
            new TenantLookupDto(tenant.Id, "Acme", "acme", "en"),
            result.Value);
    }

    [Fact]
    public async Task GetBySubdomain_Unknown_ReturnsTenantNotFound()
    {
        var result = await new GetTenantBySubdomainQueryHandler(_tenants)
            .HandleAsync(new GetTenantBySubdomainQuery("missing"));

        Assert.False(result.IsSuccess);
        Assert.Equal(TenantErrorCodes.TenantNotFound, result.Error!.Code);
    }

    [Fact]
    public async Task GetBySubdomain_Invalid_ReturnsSubdomainInvalid()
    {
        var result = await new GetTenantBySubdomainQueryHandler(_tenants)
            .HandleAsync(new GetTenantBySubdomainQuery("admin"));

        Assert.False(result.IsSuccess);
        Assert.Equal(TenantErrorCodes.SubdomainReserved, result.Error!.Code);
    }
}
