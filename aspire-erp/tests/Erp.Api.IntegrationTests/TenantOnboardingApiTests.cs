using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Erp.Api.IntegrationTests;

/// <summary>
/// Multi-tenant subdomain onboarding acceptance against the LIVE dev SQL container:
/// registration builds tenant + company + COA + RBAC + admin atomically, host resolution finds
/// the tenant through {subdomain}.domain.com, the header remains an override, and a failure
/// after the tenant insert rolls the whole onboarding back.
/// </summary>
public sealed class TenantOnboardingApiTests : IClassFixture<ErpApiFactory>, IAsyncLifetime
{
    private const string Password = "TenantAdmin123!";
    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web);

    private readonly ErpApiFactory _factory;
    private readonly string _tag = Guid.NewGuid().ToString("N")[..8];
    private readonly List<string> _subdomains = [];

    public TenantOnboardingApiTests(ErpApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Register_BuildsEverything_AndResolvesTenantFromHost()
    {
        var subdomain = NewSubdomain("ob");
        var email = $"admin-{subdomain}@example.com";

        using var client = _factory.CreateClient();
        using var response = await RegisterAsync(client, subdomain, email, "en");

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"Register {(int)response.StatusCode}: {body}");

        var json = JsonNode.Parse(body)!;
        var tenantId = json["tenantId"]!.GetValue<Guid>();
        var companyId = json["companyId"]!.GetValue<Guid>();
        var adminId = json["adminUserId"]!.GetValue<Guid>();

        Assert.Equal(subdomain, json["subdomain"]!.GetValue<string>());
        Assert.Equal(email, json["adminEmail"]!.GetValue<string>());
        Assert.Equal("en", json["defaultLanguage"]!.GetValue<string>());

        // Public availability + metadata now agree.
        using var availability = await client.GetAsync(
            $"/api/v1/tenants/check-availability?subdomain={subdomain}");
        var availabilityJson = JsonNode.Parse(await availability.Content.ReadAsStringAsync())!;
        Assert.False(availabilityJson["available"]!.GetValue<bool>());

        using var lookup = await client.GetAsync($"/api/v1/tenants/by-subdomain/{subdomain}");
        var lookupJson = JsonNode.Parse(await lookup.Content.ReadAsStringAsync())!;
        Assert.Equal(tenantId, lookupJson["id"]!.GetValue<Guid>());
        Assert.Equal("en", lookupJson["defaultLanguage"]!.GetValue<string>());

        // Persisted aggregate: tenant, one company, 23 standard accounts, six roles, admin and
        // the localized starter catalogs.
        Assert.Equal(1, await ScalarAsync(
            "SELECT COUNT(*) FROM dbo.Tenant WHERE Id = @tenant AND Subdomain = @subdomain;",
            ("@tenant", tenantId), ("@subdomain", subdomain)));
        Assert.Equal("en", await ScalarStringAsync(
            "SELECT DefaultLanguage FROM dbo.Tenant WHERE Id = @tenant;",
            ("@tenant", tenantId)));
        Assert.Equal(1, await ScalarAsync(
            "SELECT COUNT(*) FROM dbo.Company WHERE Id = @company AND TenantId = @tenant;",
            ("@company", companyId), ("@tenant", tenantId)));
        Assert.Equal(23, await ScalarAsync(
            "SELECT COUNT(*) FROM dbo.Account WHERE TenantId = @tenant;",
            ("@tenant", tenantId)));
        Assert.Equal(4, await ScalarAsync(
            "SELECT COUNT(*) FROM dbo.UOM WHERE TenantId = @tenant;",
            ("@tenant", tenantId)));
        Assert.Equal(2, await ScalarAsync(
            "SELECT COUNT(*) FROM dbo.Warehouse WHERE TenantId = @tenant;",
            ("@tenant", tenantId)));
        Assert.Equal(2, await ScalarAsync(
            "SELECT COUNT(*) FROM dbo.AssetCategory WHERE TenantId = @tenant;",
            ("@tenant", tenantId)));
        Assert.Equal(6, await ScalarAsync(
            "SELECT COUNT(*) FROM dbo.Roles WHERE TenantId = @tenant;",
            ("@tenant", tenantId)));
        Assert.True(await ScalarAsync(
            "SELECT COUNT(*) FROM dbo.DocTypePermissions WHERE TenantId = @tenant;",
            ("@tenant", tenantId)) > 0);
        Assert.Equal("Assets", await ScalarStringAsync(
            "SELECT AccountName FROM dbo.Account WHERE TenantId = @tenant AND AccountCode = '1000';",
            ("@tenant", tenantId)));
        Assert.Equal("Each", await ScalarStringAsync(
            "SELECT UomName FROM dbo.UOM WHERE TenantId = @tenant AND Symbol = 'EA';",
            ("@tenant", tenantId)));
        Assert.Equal("All Warehouses", await ScalarStringAsync(
            "SELECT WarehouseName FROM dbo.Warehouse WHERE TenantId = @tenant AND WarehouseCode = 'WH';",
            ("@tenant", tenantId)));
        Assert.Equal("IT Hardware", await ScalarStringAsync(
            "SELECT CategoryName FROM dbo.AssetCategory WHERE TenantId = @tenant AND CategoryName = 'IT Hardware';",
            ("@tenant", tenantId)));
        Assert.Equal("Unrestricted access (authorization bypass).", await ScalarStringAsync(
            "SELECT Description FROM dbo.Roles WHERE TenantId = @tenant AND Name = 'System Manager';",
            ("@tenant", tenantId)));
        Assert.Equal(1, await ScalarAsync(
            """
            SELECT COUNT(*) FROM dbo.Account child
            INNER JOIN dbo.Account parent ON parent.Id = child.ParentAccountId
            WHERE child.TenantId = @tenant AND child.AccountCode = '1120' AND parent.AccountCode = '1000';
            """,
            ("@tenant", tenantId)));
        Assert.Equal("2120", await ScalarStringAsync(
            "SELECT StockReceivedAccountCode FROM dbo.Company WHERE Id = @company;",
            ("@company", companyId)));
        Assert.Equal(1, await ScalarAsync(
            """
            SELECT COUNT(*) FROM dbo.UserRoles ur
            INNER JOIN dbo.Roles r ON r.Id = ur.RoleId
            WHERE ur.TenantId = @tenant AND ur.UserId = @admin AND r.Name = 'System Manager';
            """,
            ("@tenant", tenantId), ("@admin", adminId)));

        var storedHash = await ScalarStringAsync(
            "SELECT PasswordHash FROM dbo.Users WHERE Id = @admin;",
            ("@admin", adminId));
        Assert.StartsWith("v1.", storedHash);
        Assert.DoesNotContain(Password, storedHash);

        // The administrator can sign in with the password chosen on the form.
        using var loginClient = _factory.CreateClient();
        loginClient.DefaultRequestHeaders.Add("X-Tenant-ID", tenantId.ToString());
        using var login = await loginClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email, password = Password },
            PayloadOptions);
        Assert.True(
            login.StatusCode == HttpStatusCode.OK,
            $"Login {(int)login.StatusCode}: {await login.Content.ReadAsStringAsync()}");

        // Host-based resolution: no X-Tenant-ID header, only the subdomain in Host.
        using var hostClient = _factory.CreateClient();
        hostClient.DefaultRequestHeaders.Host = $"{subdomain}.domain.com";
        using var users = await hostClient.GetAsync("/api/v1/users?page=1&pageSize=20");
        var usersBody = await users.Content.ReadAsStringAsync();
        Assert.True(users.StatusCode == HttpStatusCode.OK, $"Host users {(int)users.StatusCode}: {usersBody}");
        Assert.Contains(
            email,
            JsonNode.Parse(usersBody)!["items"]!
                .AsArray()
                .Select(node => node!["email"]!.GetValue<string>()),
            StringComparer.OrdinalIgnoreCase);

        // Header override wins even when the Host names an unknown subdomain.
        using var overrideClient = _factory.CreateClient();
        overrideClient.DefaultRequestHeaders.Host = $"unknown-{subdomain}.domain.com";
        overrideClient.DefaultRequestHeaders.Add("X-Tenant-ID", tenantId.ToString());
        using var overridden = await overrideClient.GetAsync("/api/v1/users?page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, overridden.StatusCode);
    }

    [Fact]
    public async Task Register_WithSpanishDefaultLanguage_SeedsSpanishMasters_AndRoundTrips()
    {
        var subdomain = NewSubdomain("es");
        var email = $"admin-{subdomain}@example.com";

        using var client = _factory.CreateClient();
        using var response = await RegisterAsync(client, subdomain, email, "es");

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"Register {(int)response.StatusCode}: {body}");

        var json = JsonNode.Parse(body)!;
        var tenantId = json["tenantId"]!.GetValue<Guid>();
        Assert.Equal("es", json["defaultLanguage"]!.GetValue<string>());

        using var lookup = await client.GetAsync($"/api/v1/tenants/by-subdomain/{subdomain}");
        var lookupJson = JsonNode.Parse(await lookup.Content.ReadAsStringAsync())!;
        Assert.Equal("es", lookupJson["defaultLanguage"]!.GetValue<string>());

        Assert.Equal("es", await ScalarStringAsync(
            "SELECT DefaultLanguage FROM dbo.Tenant WHERE Id = @tenant;",
            ("@tenant", tenantId)));
        Assert.Equal("Activos", await ScalarStringAsync(
            "SELECT AccountName FROM dbo.Account WHERE TenantId = @tenant AND AccountCode = '1000';",
            ("@tenant", tenantId)));
        Assert.Equal("Unit", await ScalarStringAsync(
            "SELECT UomName FROM dbo.UOM WHERE TenantId = @tenant AND Symbol = 'PZA';",
            ("@tenant", tenantId)));
        Assert.Equal("Todos los Almacenes", await ScalarStringAsync(
            "SELECT WarehouseName FROM dbo.Warehouse WHERE TenantId = @tenant AND WarehouseCode = 'WH';",
            ("@tenant", tenantId)));
        Assert.Equal("Equipo de Cómputo y TI", await ScalarStringAsync(
            "SELECT CategoryName FROM dbo.AssetCategory WHERE TenantId = @tenant AND CategoryName = 'Equipo de Cómputo y TI';",
            ("@tenant", tenantId)));
        Assert.Equal("Acceso total sin restricciones al sistema.", await ScalarStringAsync(
            "SELECT Description FROM dbo.Roles WHERE TenantId = @tenant AND Name = 'System Manager';",
            ("@tenant", tenantId)));
    }

    [Fact]
    public async Task Register_DuplicateSubdomain_Returns409_AndKeepsSingleTenant()
    {
        var subdomain = NewSubdomain("dup");

        using var client = _factory.CreateClient();
        using var first = await RegisterAsync(client, subdomain, $"first-{subdomain}@example.com");
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        using var second = await RegisterAsync(client, subdomain, $"second-{subdomain}@example.com");
        var body = await second.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(TenantErrorCodes.SubdomainTaken, CodeOf(body));
        Assert.Equal(1, await ScalarAsync(
            "SELECT COUNT(*) FROM dbo.Tenant WHERE Subdomain = @subdomain;",
            ("@subdomain", subdomain)));
    }

    [Theory]
    [InlineData("admin", TenantErrorCodes.SubdomainReserved)]
    [InlineData("ab", TenantErrorCodes.SubdomainInvalid)]
    [InlineData("-bad", TenantErrorCodes.SubdomainInvalid)]
    public async Task Register_InvalidOrReservedSubdomain_Returns400(
        string subdomain,
        string expectedCode)
    {
        using var client = _factory.CreateClient();
        using var response = await RegisterAsync(
            client,
            subdomain,
            $"invalid-{_tag}@example.com");

        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(expectedCode, CodeOf(body));
        Assert.Equal(0, await ScalarAsync(
            "SELECT COUNT(*) FROM dbo.Tenant WHERE Subdomain = @subdomain;",
            ("@subdomain", subdomain)));
    }

    [Fact]
    public async Task Repository_FailureAfterTenantInsert_RollsBackWholeOnboarding()
    {
        var subdomain = NewSubdomain("rb");

        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITenantOnboardingRepository>();

        // Bypass the command validator on purpose: a 60-char TaxId makes SQL Server fail on the
        // Company insert AFTER the Tenant insert was staged in the same transaction.
        await Assert.ThrowsAsync<DbUpdateException>(() => repository.RegisterAsync(
            new TenantRegistrationData(
                TenantName: "Rollback Tenant",
                TenantCode: subdomain.ToUpperInvariant(),
                Subdomain: subdomain,
                CompanyName: "Rollback Company",
                CompanyTaxId: new string('X', 60),
                AdminFullName: "Rollback Admin",
                AdminEmail: $"rollback-{subdomain}@example.com",
                AdminPasswordHash: "v1.1.salt.hash")));

        Assert.Equal(0, await ScalarAsync(
            "SELECT COUNT(*) FROM dbo.Tenant WHERE Subdomain = @subdomain;",
            ("@subdomain", subdomain)));
        Assert.Equal(0, await ScalarAsync(
            "SELECT COUNT(*) FROM dbo.Company WHERE Name = 'Rollback Company';"));
        Assert.Equal(0, await ScalarAsync(
            "SELECT COUNT(*) FROM dbo.Users WHERE Email = @email;",
            ("@email", $"rollback-{subdomain}@example.com")));
    }

    public async Task DisposeAsync()
    {
        if (_subdomains.Count == 0)
        {
            return;
        }

        await using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();

        var list = string.Join(", ", _subdomains.Select((_, index) => $"@s{index}"));

        foreach (var sql in new[]
                 {
                     $"DELETE ur FROM dbo.UserRoles ur INNER JOIN dbo.Tenant t ON t.Id = ur.TenantId WHERE t.Subdomain IN ({list});",
                     $"DELETE u FROM dbo.Users u INNER JOIN dbo.Tenant t ON t.Id = u.TenantId WHERE t.Subdomain IN ({list});",
                     $"DELETE p FROM dbo.DocTypePermissions p INNER JOIN dbo.Tenant t ON t.Id = p.TenantId WHERE t.Subdomain IN ({list});",
                     $"DELETE r FROM dbo.Roles r INNER JOIN dbo.Tenant t ON t.Id = r.TenantId WHERE t.Subdomain IN ({list});",
                     $"DELETE ac FROM dbo.AssetCategory ac INNER JOIN dbo.Tenant t ON t.Id = ac.TenantId WHERE t.Subdomain IN ({list});",
                     $"DELETE w FROM dbo.Warehouse w INNER JOIN dbo.Tenant t ON t.Id = w.TenantId WHERE t.Subdomain IN ({list});",
                     $"DELETE u FROM dbo.UOM u INNER JOIN dbo.Tenant t ON t.Id = u.TenantId WHERE t.Subdomain IN ({list});",
                     $"DELETE a FROM dbo.Account a INNER JOIN dbo.Tenant t ON t.Id = a.TenantId WHERE t.Subdomain IN ({list});",
                     $"DELETE c FROM dbo.Company c INNER JOIN dbo.Tenant t ON t.Id = c.TenantId WHERE t.Subdomain IN ({list});",
                     $"DELETE FROM dbo.Tenant WHERE Subdomain IN ({list});",
                 })
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddRange(
                _subdomains
                    .Select((value, index) => new SqlParameter($"@s{index}", value))
                    .ToArray());
            await command.ExecuteNonQueryAsync();
        }
    }

    private string NewSubdomain(string prefix)
    {
        var subdomain = $"{prefix}{_tag}";
        _subdomains.Add(subdomain);
        return subdomain;
    }

    private static Task<HttpResponseMessage> RegisterAsync(
        HttpClient client,
        string subdomain,
        string email,
        string defaultLanguage = "es") =>
        client.PostAsJsonAsync(
            "/api/v1/tenants/register",
            new
            {
                tenantName = $"Tenant {subdomain}",
                companyName = $"Company {subdomain}",
                subdomain,
                adminFullName = "Onboarding Admin",
                adminEmail = email,
                adminPassword = Password,
                companyTaxId = $"TAX-{subdomain}",
                defaultLanguage,
            },
            PayloadOptions);

    private static string? CodeOf(string body) =>
        JsonNode.Parse(body)?["code"]?.GetValue<string>();

    private static async Task<int> ScalarAsync(
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<string?> ScalarStringAsync(
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return await command.ExecuteScalarAsync() as string;
    }
}
