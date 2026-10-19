using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Erp.Infrastructure.Seeders;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Erp.Api.IntegrationTests;

/// <summary>
/// User Management acceptance against the LIVE dev SQL container (spec USER-G2/G4):
/// an administrator creates a tenant user, the user signs in with the supplied temporary
/// password, deactivation revokes sign-in, and the last active System Manager cannot be
/// deactivated. Every row created here uses the <c>um-</c> prefix and is deleted on dispose.
/// </summary>
public class UserManagementApiTests : IClassFixture<ErpApiFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web);

    private readonly ErpApiFactory _factory;
    private readonly string _tag = $"um-{Guid.NewGuid():N}"[..12];

    public UserManagementApiTests(ErpApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreatedUser_CanSignIn_AndDeactivationRevokesAccess()
    {
        const string password = "Temporal123!X";
        var email = $"{_tag}-login@example.com";

        using var admin = CreateAdminClient();

        using var created = await admin.PostAsJsonAsync("/api/v1/users", new
        {
            fullName = "Integration Login",
            email = $"  {email.ToUpperInvariant()}  ",
            temporaryPassword = password,
            roleIds = new[] { SecurityRoleSeeder.SalesUserRoleId },
            isActive = true,
        }, PayloadOptions);

        var createdBody = await created.Content.ReadAsStringAsync();
        Assert.True(
            created.StatusCode == HttpStatusCode.Created,
            $"Create user {(int)created.StatusCode}: {createdBody}");

        var createdJson = JsonNode.Parse(createdBody)!;
        Assert.Equal(email, createdJson["user"]!["email"]!.GetValue<string>());
        Assert.Equal(password, createdJson["temporaryPassword"]!.GetValue<string>());
        var userId = createdJson["user"]!["id"]!.GetValue<Guid>();

        using var loginClient = CreateAnonymousClient();
        using var login = await SignInAsync(loginClient, email, password);

        var loginBody = await login.Content.ReadAsStringAsync();
        Assert.True(login.StatusCode == HttpStatusCode.OK, $"Login {(int)login.StatusCode}: {loginBody}");
        Assert.False(string.IsNullOrWhiteSpace(JsonNode.Parse(loginBody)!["token"]!.GetValue<string>()));

        using var deactivated = await admin.PatchAsJsonAsync(
            $"/api/v1/users/{userId}/status",
            new { isActive = false },
            PayloadOptions);

        var deactivatedBody = await deactivated.Content.ReadAsStringAsync();
        Assert.True(
            deactivated.StatusCode == HttpStatusCode.OK,
            $"Deactivate {(int)deactivated.StatusCode}: {deactivatedBody}");
        Assert.False(JsonNode.Parse(deactivatedBody)!["isActive"]!.GetValue<bool>());

        using var rejected = await SignInAsync(loginClient, email, password);
        var rejectedBody = await rejected.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
        Assert.Equal(
            "AUTH_ACCOUNT_INACTIVE",
            JsonNode.Parse(rejectedBody)!["type"]!.GetValue<string>());
    }

    [Fact]
    public async Task Deactivating_TheLastActiveSystemManager_Returns409()
    {
        using var admin = CreateAdminClient();

        using var created = await admin.PostAsJsonAsync("/api/v1/users", new
        {
            fullName = "Integration Admin",
            email = $"{_tag}-admin@example.com",
            temporaryPassword = "Temporal123!X",
            roleIds = new[] { SecurityRoleSeeder.SystemManagerRoleId },
            isActive = true,
        }, PayloadOptions);

        var createdBody = await created.Content.ReadAsStringAsync();
        Assert.True(
            created.StatusCode == HttpStatusCode.Created,
            $"Create admin {(int)created.StatusCode}: {createdBody}");
        var adminId = JsonNode.Parse(createdBody)!["user"]!["id"]!.GetValue<Guid>();

        // With the new administrator active, the seeded one is no longer the last: allowed.
        using var first = await admin.PatchAsJsonAsync(
            $"/api/v1/users/{adminId}/status",
            new { isActive = false },
            PayloadOptions);
        Assert.True(
            first.StatusCode == HttpStatusCode.OK,
            $"First deactivation {(int)first.StatusCode}: {await first.Content.ReadAsStringAsync()}");

        // Only the seeded demo administrator remains active; removing it must be refused.
        using var lookup = await admin.GetAsync(
            "/api/v1/users?page=1&pageSize=20&search=demo@example.com");
        var lookupBody = await lookup.Content.ReadAsStringAsync();
        Assert.True(lookup.StatusCode == HttpStatusCode.OK, $"Lookup {(int)lookup.StatusCode}: {lookupBody}");

        var seededAdmin = JsonNode.Parse(lookupBody)!["items"]!
            .AsArray()
            .Select(node => node!)
            .FirstOrDefault(node =>
                string.Equals(node["email"]!.GetValue<string>(), "demo@example.com", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(seededAdmin);

        using var rejected = await admin.PatchAsJsonAsync(
            $"/api/v1/users/{seededAdmin!["id"]!.GetValue<Guid>()}/status",
            new { isActive = false },
            PayloadOptions);

        var rejectedBody = await rejected.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        Assert.Equal(
            "CANNOT_REMOVE_LAST_ADMIN",
            JsonNode.Parse(rejectedBody)!["code"]!.GetValue<string>());
    }

    public async Task DisposeAsync()
    {
        await using var connection = new SqlConnection(ErpApiFactory.DevConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            "DELETE FROM dbo.Users WHERE TenantId = @TenantId AND Email LIKE @Prefix;";
        command.Parameters.AddWithValue("@TenantId", ErpApiFactory.DevTenantId);
        command.Parameters.AddWithValue("@Prefix", $"{_tag}-%");
        await command.ExecuteNonQueryAsync();
    }

    private HttpClient CreateAdminClient()
    {
        var client = _factory.CreateClient();
        // TestAuthHandler defaults to the System Manager bypass.
        client.DefaultRequestHeaders.Add("X-Tenant-ID", ErpApiFactory.DevTenantId.ToString());
        return client;
    }

    private HttpClient CreateAnonymousClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-ID", ErpApiFactory.DevTenantId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.NoAuthHeader, "1");
        return client;
    }

    private static Task<HttpResponseMessage> SignInAsync(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }, PayloadOptions);
}
