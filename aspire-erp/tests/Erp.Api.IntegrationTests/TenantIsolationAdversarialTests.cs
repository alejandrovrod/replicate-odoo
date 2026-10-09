using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Erp.Application.Common;
using Erp.Domain.Entities;
using Erp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Api.IntegrationTests;

/// <summary>
/// Task 8.1 acceptance against the LIVE dev SQL container: five tenants drive drafts
/// (customers, suppliers, sales orders, sales invoices, payment entries) CONCURRENTLY, and
/// every read must return strictly tenant-isolated rows (zero leakage). A second wave plays
/// the adversary: cross-tenant primary-key reads, forged company scopes and TenantId
/// tampering at the change-tracker level must all fail closed.
/// </summary>
/// <remarks>
/// <para><b>No ledger writes by design.</b> Every document stays in Draft (zero StockLedgerEntry
/// and zero GLEntry impact), so this class stays OUT of <c>LedgerMutatingCollection</c> and can
/// run in parallel with every other suite. Masters (tenant, company, UOM, item, GL leaf, bank
/// profile) are provisioned per tenant with IDENTICAL codes on purpose: same-code rows in five
/// tenants are the strongest isolation shape - any leak is immediately visible.</para>
///
/// <para><b>Cleanup.</b> DisposeAsync deletes masters and drafts in FK-safe order through
/// tenant-pinned contexts; ledger tables are never touched because drafts write none.</para>
/// </remarks>
public class TenantIsolationAdversarialTests : IClassFixture<ErpApiFactory>, IAsyncLifetime
{
    private const int TenantCount = 5;

    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web);

    private readonly ErpApiFactory _factory;
    private readonly Guid[] _tenants = Enumerable.Range(0, TenantCount).Select(_ => Guid.NewGuid()).ToArray();
    private readonly ConcurrentDictionary<Guid, TenantWorld> _worlds = new();

    public TenantIsolationAdversarialTests(ErpApiFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        foreach (var tenantId in _tenants)
        {
            _worlds[tenantId] = await ProvisionTenantAsync(tenantId);
        }
    }

    /// <summary>
    /// Five tenants concurrently create one customer, one supplier, one sales order, one sales
    /// invoice and one payment draft each; every list endpoint must then return EXACTLY the
    /// caller's own row - no more (leak), no fewer (cross-tenant interference).
    /// </summary>
    [Fact]
    public async Task ConcurrentDrafts_AcrossFiveTenants_StayStrictlyIsolated()
    {
        await Task.WhenAll(_tenants.Select(tenantId => Task.Run(() => DriveTenantAsync(tenantId))));

        foreach (var tenantId in _tenants)
        {
            using var client = CreateClient(tenantId);
            var world = _worlds[tenantId];

            var customers = await GetItemsAsync(client, $"/api/v1/customers?companyId={world.CompanyId}");
            Assert.Single(customers);
            Assert.Equal(world.CustomerId, IdOf(customers[0]));

            var suppliers = await GetItemsAsync(client, "/api/v1/suppliers?page=1&pageSize=50");
            Assert.Single(suppliers);
            Assert.Equal(world.SupplierId, IdOf(suppliers[0]));

            var orders = await GetItemsAsync(client, $"/api/v1/sales-orders?companyId={world.CompanyId}");
            Assert.Single(orders);
            Assert.Equal(world.OrderId, IdOf(orders[0]));

            var invoices = await GetItemsAsync(client, $"/api/v1/sales-invoices?companyId={world.CompanyId}");
            Assert.Single(invoices);
            Assert.Equal(world.InvoiceId, IdOf(invoices[0]));

            var payments = await GetItemsAsync(client, $"/api/v1/payment-entries?companyId={world.CompanyId}");
            Assert.Single(payments);
            Assert.Equal(world.PaymentId, IdOf(payments[0]));

            var pOrders = await GetItemsAsync(client, $"/api/v1/purchase-orders?companyId={world.CompanyId}");
            Assert.Single(pOrders);
            Assert.Equal(world.PurchaseOrderId, IdOf(pOrders[0]));

            var pInvoices = await GetItemsAsync(client, $"/api/v1/purchase-invoices?companyId={world.CompanyId}");
            Assert.Single(pInvoices);
            Assert.Equal(world.PurchaseInvoiceId, IdOf(pInvoices[0]));
        }
    }

    /// <summary>
    /// The adversary wave: primary-key reads across tenants 404, a forged company scope answers
    /// empty, and a body scoped to another tenant's company is rejected - never leaked.
    /// </summary>
    [Fact]
    public async Task CrossTenantAccess_ReturnsNotFoundOrEmpty_NeverRows()
    {
        await Task.WhenAll(_tenants.Select(tenantId => Task.Run(() => DriveTenantAsync(tenantId))));

        var first = _tenants[0];
        var second = _tenants[1];
        var firstWorld = _worlds[first];
        var secondWorld = _worlds[second];

        using var intruder = CreateClient(second);

        // Same companyId the victim owns, but the intruder's tenant cannot see the company:
        // the order endpoint resolves nothing and 404s.
        using var byId = await intruder.GetAsync(
            $"/api/v1/sales-orders/{firstWorld.OrderId}?companyId={firstWorld.CompanyId}");
        Assert.Equal(HttpStatusCode.NotFound, byId.StatusCode);

        // Forged list scope: the victim's company under the intruder's header is an empty page.
        var leaked = await GetItemsAsync(intruder, $"/api/v1/sales-orders?companyId={firstWorld.CompanyId}");
        Assert.Empty(leaked);

        var leakedInvoices = await GetItemsAsync(intruder, $"/api/v1/sales-invoices?companyId={firstWorld.CompanyId}");
        Assert.Empty(leakedInvoices);

        var leakedPayments = await GetItemsAsync(intruder, $"/api/v1/payment-entries?companyId={firstWorld.CompanyId}");
        Assert.Empty(leakedPayments);

        var leakedPOrders = await GetItemsAsync(intruder, $"/api/v1/purchase-orders?companyId={firstWorld.CompanyId}");
        Assert.Empty(leakedPOrders);

        var leakedPInvoices = await GetItemsAsync(intruder, $"/api/v1/purchase-invoices?companyId={firstWorld.CompanyId}");
        Assert.Empty(leakedPInvoices);

        var leakedCustomers = await GetItemsAsync(intruder, $"/api/v1/customers?companyId={firstWorld.CompanyId}");
        Assert.Empty(leakedCustomers);

        // Forged write scope: an order body naming the victim's company is rejected (4xx), and
        // the victim's list is still exactly one row afterwards.
        using var forged = await PostJsonAsync(intruder, "/api/v1/sales-orders", new
        {
            companyId = firstWorld.CompanyId,
            customerId = secondWorld.CustomerId,
            transactionDate = Format(DateOnly.FromDateTime(DateTime.UtcNow)),
            deliveryDate = Format(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7)),
            lines = new[] { new { itemId = secondWorld.ItemId, quantity = 1m, rate = 10m } },
        });
        Assert.True(
            (int)forged.StatusCode >= 400 && (int)forged.StatusCode < 500,
            $"Forged-company order must be rejected, was {(int)forged.StatusCode}");

        using var victim = CreateClient(first);
        var victimOrders = await GetItemsAsync(victim, $"/api/v1/sales-orders?companyId={firstWorld.CompanyId}");
        Assert.Single(victimOrders);
    }

    /// <summary>
    /// Change-tracker and query-filter level attacks through <c>AppDbContext</c> directly:
    /// a tenant-B context sees ZERO rows of tenant A (global filter), retagging TenantId on a
    /// tracked row throws, inserting without a tenant throws, and only the
    /// filter-bypassing backdoor sees both sides (which documents that the filter - not luck -
    /// is the enforcement point).
    /// </summary>
    [Fact]
    public async Task DirectContext_AdversarialAttempts_FailClosed()
    {
        await Task.WhenAll(_tenants.Select(tenantId => Task.Run(() => DriveTenantAsync(tenantId))));

        var first = _tenants[0];
        var second = _tenants[1];
        var secondWorld = _worlds[second];

        // Global query filter: tenant B observes ONLY its own rows - none of tenant A's
        // masters or drafts (same-code rows coexist in five tenants; the filter separates them).
        await using (var intruder = CreateContext(second))
        {
            var customers = await intruder.Customers.ToListAsync();
            Assert.Single(customers);
            Assert.All(customers, c => Assert.Equal(second, c.TenantId));

            var suppliers = await intruder.Suppliers.ToListAsync();
            Assert.Single(suppliers);
            Assert.All(suppliers, s => Assert.Equal(second, s.TenantId));

            var orders = await intruder.SalesOrders.ToListAsync();
            Assert.Single(orders);
            Assert.All(orders, o => Assert.Equal(secondWorld.CompanyId, o.CompanyId));

            var invoices = await intruder.SalesInvoices.ToListAsync();
            Assert.Single(invoices);

            var payments = await intruder.PaymentEntries.ToListAsync();
            Assert.Single(payments);

            var pOrders = await intruder.PurchaseOrders.ToListAsync();
            Assert.Single(pOrders);
            Assert.All(pOrders, o => Assert.Equal(secondWorld.CompanyId, o.CompanyId));

            var pInvoices = await intruder.PurchaseInvoices.ToListAsync();
            Assert.Single(pInvoices);

            var companies = await intruder.Companies.ToListAsync();
            Assert.Single(companies);

            // Primary-key lookup of the victim's customer through the intruder's filter: null.
            Assert.Null(await intruder.Customers.FirstOrDefaultAsync(c => c.Id == _worlds[first].CustomerId));
        }

        // Retagging TenantId on a tracked row is forbidden (Constitution Article II.4).
        await using (var tamperer = CreateContext(first))
        {
            var customer = await tamperer.Customers.FirstAsync(c => c.Id == _worlds[first].CustomerId);
            tamperer.Entry(customer).Property(c => c.TenantId).CurrentValue = second;

            await Assert.ThrowsAsync<InvalidOperationException>(() => tamperer.SaveChangesAsync());
        }

        // No tenant context at all: inserts fail closed instead of landing tenant-less.
        await using (var orphan = CreateContext(Guid.Empty))
        {
            orphan.Customers.Add(new Customer
            {
                Id = Guid.NewGuid(),
                CompanyId = _worlds[first].CompanyId,
                CustomerCode = "T8A-ORPHAN",
                CustomerName = "Orphan",
            });

            await Assert.ThrowsAsync<InvalidOperationException>(() => orphan.SaveChangesAsync());
        }

        // Backdoor check: with filters bypassed, all FIVE tenants' rows exist - proving the
        // rows are really there and only the global filter hides them from each tenant.
        // (Scoped to our tenant ids: sibling suites concurrently write their own customers.)
        await using (var backdoor = CreateContext(first))
        {
            var all = await backdoor.Customers
                .IgnoreQueryFilters()
                .Where(c => _tenants.Contains(c.TenantId))
                .CountAsync();
            Assert.Equal(TenantCount, all);
        }
    }

    public async Task DisposeAsync()
    {
        foreach (var tenantId in _tenants)
        {
            if (!_worlds.TryGetValue(tenantId, out _))
            {
                continue;
            }

            await using var context = CreateContext(tenantId);

            var payments = await context.PaymentEntries.Include(p => p.Allocations).ToListAsync();
            foreach (var payment in payments)
            {
                context.PaymentAllocations.RemoveRange(payment.Allocations);
            }
            context.PaymentEntries.RemoveRange(payments);

            var invoices = await context.SalesInvoices.Include(i => i.Items).ToListAsync();
            foreach (var invoice in invoices)
            {
                context.SalesInvoiceItems.RemoveRange(invoice.Items);
            }
            context.SalesInvoices.RemoveRange(invoices);

            var pInvoices = await context.PurchaseInvoices.Include(i => i.Items).ToListAsync();
            foreach (var pInvoice in pInvoices)
            {
                context.PurchaseInvoiceItems.RemoveRange(pInvoice.Items);
            }
            context.PurchaseInvoices.RemoveRange(pInvoices);

            var orders = await context.SalesOrders.Include(o => o.Lines).ToListAsync();
            foreach (var order in orders)
            {
                context.RemoveRange(order.Lines);
            }
            context.SalesOrders.RemoveRange(orders);

            var pOrders = await context.PurchaseOrders.Include(o => o.Lines).ToListAsync();
            foreach (var pOrder in pOrders)
            {
                context.RemoveRange(pOrder.Lines);
            }
            context.PurchaseOrders.RemoveRange(pOrders);

            context.Customers.RemoveRange(context.Customers);
            context.Suppliers.RemoveRange(context.Suppliers);
            context.BankAccounts.RemoveRange(context.BankAccounts);
            context.Items.RemoveRange(context.Items);
            context.UOMs.RemoveRange(context.UOMs);
            context.Accounts.RemoveRange(context.Accounts);
            context.Companies.RemoveRange(context.Companies);
            await context.SaveChangesAsync();

            await using var root = CreateContext(tenantId);
            var tenant = await root.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId);
            if (tenant is not null)
            {
                root.Tenants.Remove(tenant);
                await root.SaveChangesAsync();
            }
        }
    }

    // ----------------------------------------------------------------------- provisioning

    private sealed record TenantWorld(
        Guid CompanyId,
        Guid UomId,
        Guid ItemId,
        Guid AccountId,
        Guid BankAccountId,
        Guid CustomerId,
        Guid SupplierId,
        Guid OrderId,
        Guid InvoiceId,
        Guid PaymentId,
        Guid PurchaseOrderId,
        Guid PurchaseInvoiceId);

    /// <summary>Direct-context masters for one tenant (no HTTP): tenant, company, UOM, GL leaf, bank profile.</summary>
    private static async Task<TenantWorld> ProvisionTenantAsync(Guid tenantId)
    {
        var companyId = Guid.NewGuid();
        var uomId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var bankAccountId = Guid.NewGuid();

        await using (var context = CreateContext(tenantId))
        {
            context.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = $"T8A Tenant {tenantId:N}"[..32],
                Code = $"T8A-{tenantId:N}"[..16],
                CreatedAt = DateTimeOffset.UtcNow,
            });
            context.Companies.Add(new Company
            {
                Id = companyId,
                Name = "T8A Company",
                TaxId = "T8A-TAX",
                CreatedAt = DateTimeOffset.UtcNow,
            });
            context.UOMs.Add(new UOM
            {
                Id = uomId,
                CompanyId = companyId,
                UomName = "Unit",
                Symbol = "u",
            });
            context.Accounts.Add(new Account
            {
                Id = accountId,
                CompanyId = companyId,
                AccountCode = "T8A-CASH",
                AccountName = "T8A Cash",
                RootType = AccountRootType.Asset,
                IsGroup = false,
                IsActive = true,
            });
            context.BankAccounts.Add(new BankAccount
            {
                Id = bankAccountId,
                CompanyId = companyId,
                AccountName = "T8A Checking",
                BankName = "T8A Bank",
                AccountNumber = $"T8A-{tenantId:N}"[..20],
                GLAccountId = accountId,
                IsActive = true,
            });
            await context.SaveChangesAsync();
        }

        return new TenantWorld(companyId, uomId, itemId, accountId, bankAccountId,
            Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty);
    }

    /// <summary>One tenant's concurrent wave over HTTP: item, customer, supplier, order, invoice and payment drafts.</summary>
    private async Task DriveTenantAsync(Guid tenantId)
    {
        var world = _worlds[tenantId];
        using var client = CreateClient(tenantId);
        var today = Format(DateOnly.FromDateTime(DateTime.UtcNow));

        var item = await PostJsonAsync(client, "/api/v1/items", new
        {
            code = "T8A-IT-001",
            name = "T8A Bracket",
            valuationMethod = "Fifo",
            baseUOMId = world.UomId,
        });
        var itemId = (await ReadOkAsync(item))["id"]!.GetValue<Guid>();

        var customer = await PostJsonAsync(client, "/api/v1/customers", new
        {
            companyId = world.CompanyId,
            customerCode = "T8A-CUST-001",
            customerName = "T8A Buyer",
        });
        var customerId = (await ReadOkAsync(customer))["id"]!.GetValue<Guid>();

        var supplier = await PostJsonAsync(client, "/api/v1/suppliers", new
        {
            code = "T8A-SUP-001",
            name = "T8A Vendor",
        });
        var supplierId = (await ReadOkAsync(supplier))["id"]!.GetValue<Guid>();

        var order = await PostJsonAsync(client, "/api/v1/sales-orders", new
        {
            companyId = world.CompanyId,
            customerId,
            transactionDate = today,
            deliveryDate = today,
            lines = new[] { new { itemId, quantity = 2m, rate = 50m } },
        });
        var orderId = (await ReadOkAsync(order))["id"]!.GetValue<Guid>();

        var invoice = await PostJsonAsync(client, "/api/v1/sales-invoices", new
        {
            companyId = world.CompanyId,
            customerId,
            postingDate = today,
            items = new[] { new { itemId, quantity = 2m, rate = 50m } },
        });
        var invoiceId = (await ReadOkAsync(invoice))["id"]!.GetValue<Guid>();

        var payment = await PostJsonAsync(client, "/api/v1/payment-entries", new
        {
            companyId = world.CompanyId,
            paymentType = "Receive",
            partyType = "Customer",
            partyId = customerId,
            bankAccountId = world.BankAccountId,
            paymentDate = today,
            paidAmount = 100m,
            transactionCurrencyId = (Guid?)null,
            referenceNumber = (string?)null,
            allocations = Array.Empty<object>(),
        }, idempotencyKey: $"T8A-{tenantId:N}");
        var paymentId = (await ReadOkAsync(payment))["id"]!.GetValue<Guid>();

        var pOrder = await PostJsonAsync(client, "/api/v1/purchase-orders", new
        {
            companyId = world.CompanyId,
            supplierId,
            transactionDate = today,
            deliveryDate = today,
            lines = new[] { new { itemId, quantity = 2m, rate = 50m } },
        });
        var purchaseOrderId = (await ReadOkAsync(pOrder))["id"]!.GetValue<Guid>();

        var pInvoice = await PostJsonAsync(client, "/api/v1/purchase-invoices", new
        {
            companyId = world.CompanyId,
            supplierId,
            postingDate = today,
            items = new[] { new { itemId, quantity = 2m, rate = 50m } },
        });
        var purchaseInvoiceId = (await ReadOkAsync(pInvoice))["id"]!.GetValue<Guid>();

        _worlds[tenantId] = world with
        {
            ItemId = itemId,
            CustomerId = customerId,
            SupplierId = supplierId,
            OrderId = orderId,
            InvoiceId = invoiceId,
            PaymentId = paymentId,
            PurchaseOrderId = purchaseOrderId,
            PurchaseInvoiceId = purchaseInvoiceId,
        };
    }

    // ----------------------------------------------------------------------- infrastructure

    private HttpClient CreateClient(Guid tenantId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-ID", tenantId.ToString());
        return client;
    }

    private static AppDbContext CreateContext(Guid tenantId) =>
        new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(ErpApiFactory.DevConnectionString)
                .Options,
            new StubTenantProvider(tenantId));

    private static async Task<JsonArray> GetItemsAsync(HttpClient client, string url)
    {
        using var response = await client.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"GET {url} {(int)response.StatusCode}: {body}");
        return JsonNode.Parse(body)!["items"]!.AsArray();
    }

    private static async Task<HttpResponseMessage> PostJsonAsync<T>(
        HttpClient client, string url, T payload, string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload, options: PayloadOptions),
        };

        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        return await client.SendAsync(request);
    }

    private static async Task<JsonObject> ReadOkAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created,
            $"POST {(int)response.StatusCode}: {body}");
        response.Dispose();
        return (JsonObject)JsonNode.Parse(body)!;
    }

    private static Guid IdOf(JsonNode node) => node["id"]!.GetValue<Guid>();

    private static string Format(DateOnly date) =>
        date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private sealed class StubTenantProvider : ITenantProvider
    {
        private Guid _tenantId;

        public StubTenantProvider(Guid tenantId) => _tenantId = tenantId;

        public Guid GetCurrentTenantId() => _tenantId;

        public bool HasTenant() => _tenantId != Guid.Empty;

        public void SetCurrentTenantId(Guid tenantId) => _tenantId = tenantId;
    }
}
