using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Erp.Application.Common;
using Erp.Domain.Entities;
using Erp.Domain.Entities.Security;
using Erp.Infrastructure.Data;
using Erp.Infrastructure.Seeders;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Api.IntegrationTests;

/// <summary>
/// Fase R3 acceptance against the LIVE dev SQL container: the dynamic
/// <c>permission:doctype:action</c> policies deny and allow exactly per the seeded matrix,
/// System Manager bypasses everything, anonymous callers get 401, and login issues JWTs
/// carrying the flattened <c>perms</c> claims.
/// </summary>
/// <remarks>
/// <para><b>No ledger writes:</b> every document stays in Draft and users/roles are cleaned
/// up, so this class serializes with nothing. The <c>TestAuthHandler</c> header contract
/// (<c>X-Test-NoAuth</c> / <c>X-Test-Roles</c> / <c>X-Test-Perms</c>) simulates JWT identities
/// without real tokens - the factory replaces the bearer scheme.</para>
/// </remarks>
public class SecurityMatrixTests : IClassFixture<ErpApiFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web);

    private static readonly Guid ItemId = Guid.Parse("c0000000-0000-4000-8000-000000000001");

    private readonly ErpApiFactory _factory;
    private readonly string _tag = $"R3-{Guid.NewGuid():N}"[..11];
    private readonly List<Guid> _customerIds = new();
    private readonly List<Guid> _orderIds = new();
    private Guid _userId = Guid.Empty;

    public SecurityMatrixTests(ErpApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => Task.CompletedTask;

    /// <summary>Seeded matrix shape: six roles on the dev tenant, System Manager with zero rows (bypass by design).</summary>
    [Fact]
    public async Task SeededMatrix_ContainsSixRoles_WithExpectedSalesPermissions()
    {
        await using var context = CreateContext(ErpApiFactory.DevTenantId);

        var names = await context.Roles
            .Where(r => r.TenantId == ErpApiFactory.DevTenantId && r.IsSystemDefault)
            .Select(r => r.Name)
            .ToListAsync();

        foreach (var expected in new[] { "System Manager", "Sales User", "Purchase User", "Accounting User", "HR User", "Manufacturing User" })
        {
            Assert.Contains(expected, names);
        }

        Assert.Empty(await context.DocTypePermissions
            .Where(p => p.TenantId == ErpApiFactory.DevTenantId && p.RoleId == SecurityRoleSeeder.SystemManagerRoleId)
            .ToListAsync());

        var salesPerms = await context.DocTypePermissions
            .Where(p => p.TenantId == ErpApiFactory.DevTenantId && p.RoleId == SecurityRoleSeeder.SalesUserRoleId)
            .ToListAsync();
        var customer = Assert.Single(salesPerms, p => p.DocType == "customer");
        Assert.True(customer.CanRead && customer.CanWrite && customer.CanSubmit);
    }

    /// <summary>No authentication at all: the permission policy challenges with 401.</summary>
    [Fact]
    public async Task AnonymousRequest_ToProtectedEndpoint_Returns401()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-ID", ErpApiFactory.DevTenantId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.NoAuthHeader, "1");

        using var response = await client.GetAsync(
            $"/api/v1/customers?companyId={ErpApiFactory.DevCompanyId}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Public surface stays public: login answers without any credentials header.</summary>
    [Fact]
    public async Task LoginEndpoint_IsAnonymous_AllowsAttemptWithoutAuth()
    {
        var userId = await CreateLoginUserAsync(SecurityRoleSeeder.SalesUserRoleId);

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-ID", ErpApiFactory.DevTenantId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.NoAuthHeader, "1");

        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = $"r3-login-{_tag}@example.com",
            password = "R3-Password!",
        }, PayloadOptions);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Login {(int)response.StatusCode}: {body}");

        // The issued JWT flattens the Sales User matrix into perms claims.
        var token = JsonNode.Parse(body)!["token"]!.GetValue<string>();
        var perms = ReadPermsClaims(token);
        Assert.Contains("customer:read", perms);
        Assert.Contains("sales_order:submit", perms);
        Assert.DoesNotContain("supplier:write", perms);

        _userId = userId;
    }

    /// <summary>Decodes the JWT payload without validating (the factory replaces bearer auth).</summary>
    private static HashSet<string> ReadPermsClaims(string token)
    {
        var payload = token.Split('.')[1]
            .Replace('-', '+')
            .Replace('_', '/');
        payload += new string('=', (4 - payload.Length % 4) % 4);
        var json = JsonNode.Parse(Convert.FromBase64String(payload))!;
        return json.AsObject()
            .Where(kvp => kvp.Key == "perms")
            .SelectMany(kvp => kvp.Value!.AsArray().Select(n => n!.GetValue<string>()))
            .ToHashSet();
    }

    /// <summary>Read gate: customer:read passes, an unrelated perm 403s, System Manager bypasses.</summary>
    [Fact]
    public async Task CustomerReadGate_EnforcesMatrixAndBypass()
    {
        using var reader = CreatePermClient("customer:read");
        using var ok = await reader.GetAsync($"/api/v1/customers?companyId={ErpApiFactory.DevCompanyId}");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        using var stranger = CreatePermClient("item:read");
        using var forbidden = await stranger.GetAsync($"/api/v1/customers?companyId={ErpApiFactory.DevCompanyId}");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        using var admin = CreateClient();
        using var bypass = await admin.GetAsync($"/api/v1/customers?companyId={ErpApiFactory.DevCompanyId}");
        Assert.Equal(HttpStatusCode.OK, bypass.StatusCode);
    }

    /// <summary>Write/submit split: creating with write works, submitting with read-only 403s, submitting with submit works.</summary>
    [Fact]
    public async Task SalesOrderSubmitGate_RequiresSubmitPerm()
    {
        var customerId = await CreateCustomerAsync("customer:write");
        var orderId = await CreateOrderAsync(customerId, "sales_order:write");

        using var reader = CreatePermClient("sales_order:read");
        using var denied = await reader.PostAsync(
            $"/api/v1/sales-orders/{orderId}/submit?companyId={ErpApiFactory.DevCompanyId}",
            new StringContent(string.Empty));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        using var submitter = CreatePermClient("sales_order:submit");
        using var allowed = await submitter.PostAsync(
            $"/api/v1/sales-orders/{orderId}/submit?companyId={ErpApiFactory.DevCompanyId}",
            new StringContent(string.Empty));
        var body = await allowed.Content.ReadAsStringAsync();
        Assert.True(allowed.StatusCode == HttpStatusCode.OK, $"Submit {(int)allowed.StatusCode}: {body}");
    }

    /// <summary>Cross-module wall: a Sales User identity cannot touch suppliers.</summary>
    [Fact]
    public async Task PurchaseEndpoints_RejectSalesIdentity_With403()
    {
        using var sales = CreatePermClient("purchase_order:read", "Sales User");
        using var response = await sales.GetAsync("/api/v1/suppliers?page=1&pageSize=5");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    public async Task DisposeAsync()
    {
        await using var context = CreateContext(ErpApiFactory.DevTenantId);

        foreach (var orderId in _orderIds)
        {
            var lines = await context.Set<SalesOrderItem>().Where(l => l.SalesOrderId == orderId).ToListAsync();
            context.Set<SalesOrderItem>().RemoveRange(lines);
            var order = await context.SalesOrders.FirstOrDefaultAsync(o => o.Id == orderId);
            if (order is not null)
            {
                context.SalesOrders.Remove(order);
            }
        }

        foreach (var customerId in _customerIds)
        {
            var customer = await context.Customers.FirstOrDefaultAsync(c => c.Id == customerId);
            if (customer is not null)
            {
                context.Customers.Remove(customer);
            }
        }

        if (_userId != Guid.Empty)
        {
            var links = await context.UserRoles.Where(ur => ur.UserId == _userId).ToListAsync();
            context.UserRoles.RemoveRange(links);
            var user = await context.Users.FirstOrDefaultAsync(u => u.Id == _userId);
            if (user is not null)
            {
                context.Users.Remove(user);
            }
        }

        await context.SaveChangesAsync();
    }

    // ----------------------------------------------------------------------- helpers

    private HttpClient CreateClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-ID", ErpApiFactory.DevTenantId.ToString());
        return client;
    }

    /// <summary>
    /// Identity with explicit perms but NO manager role (bare perms would otherwise ride the
    /// System Manager default and bypass everything under test).
    /// </summary>
    private HttpClient CreatePermClient(string perms, string roles = "Sales User")
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.PermsHeader, perms);
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, roles);
        return client;
    }

    private static AppDbContext CreateContext(Guid tenantId) =>
        new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(ErpApiFactory.DevConnectionString)
                .Options,
            new StubTenantProvider(tenantId));

    private async Task<Guid> CreateCustomerAsync(string perms)
    {
        using var client = CreatePermClient(perms);
        using var response = await client.PostAsJsonAsync("/api/v1/customers", new
        {
            companyId = ErpApiFactory.DevCompanyId,
            customerCode = $"{_tag}-CUST-{_customerIds.Count}",
            customerName = "R3 Buyer",
        }, PayloadOptions);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"Customer POST {(int)response.StatusCode}: {body}");
        var id = JsonNode.Parse(body)!["id"]!.GetValue<Guid>();
        _customerIds.Add(id);
        return id;
    }

    private async Task<Guid> CreateOrderAsync(Guid customerId, string perms)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        using var client = CreatePermClient(perms);
        using var response = await client.PostAsJsonAsync("/api/v1/sales-orders", new
        {
            companyId = ErpApiFactory.DevCompanyId,
            customerId,
            transactionDate = today,
            deliveryDate = today,
            lines = new[] { new { itemId = ItemId, quantity = 1m, rate = 10m } },
        }, PayloadOptions);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"Order POST {(int)response.StatusCode}: {body}");
        var id = JsonNode.Parse(body)!["id"]!.GetValue<Guid>();
        _orderIds.Add(id);
        return id;
    }

    private async Task<Guid> CreateLoginUserAsync(Guid roleId)
    {
        var email = $"r3-login-{_tag}@example.com";
        await using var context = CreateContext(ErpApiFactory.DevTenantId);
        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = ErpApiFactory.DevTenantId,
            Email = email,
            FullName = "R3 Login",
            PasswordHash = "R3-Password!",
        };
        context.Users.Add(user);
        context.UserRoles.Add(new UserRole
        {
            Id = Guid.NewGuid(),
            TenantId = ErpApiFactory.DevTenantId,
            UserId = user.Id,
            RoleId = roleId,
        });
        await context.SaveChangesAsync();

        return user.Id;
    }

    private sealed class StubTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;

        public StubTenantProvider(Guid tenantId) => _tenantId = tenantId;

        public Guid GetCurrentTenantId() => _tenantId;

        public bool HasTenant() => _tenantId != Guid.Empty;

        public void SetCurrentTenantId(Guid tenantId) => throw new NotSupportedException();
    }
}
