using Erp.Application.Common;
using Erp.Application.Features.Selling.Commands;
using Erp.Application.Services;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Extensibility engine Fase 1 (spec extensibility-engine, EXT-G1..EXT-G4): CustomData
/// persistence + JSON-schema validation on the Customer master, and the ordered,
/// abort-capable lifecycle pipeline on the sales submit path.
/// </summary>
public sealed class ExtensibilityEngineTests
{
    private static readonly DateOnly PostingDate = new(2026, 5, 4);

    private readonly Guid _companyId = Guid.NewGuid();

    private readonly FakeCustomerRepository _customers = new();
    private readonly FakeAccountRepository _accounts = new();
    private readonly FakeCustomFieldDefinitionRepository _definitions = new();

    private CustomFieldValidator Validator() => new(_definitions);

    private CreateCustomerCommandHandler CreateCustomerHandler() =>
        new(_customers, _accounts, new FakeCurrencyRepository(), Validator());

    private static CustomFieldDefinition RequiredString(string entity, string field) => new()
    {
        Id = Guid.NewGuid(),
        EntityName = entity,
        FieldName = field,
        FieldType = "string",
        IsRequired = true,
        IsActive = true,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    // ------------------------------------------------------------ EXT-G1 / EXT-G2 (masters)

    [Fact]
    public async Task CreateCustomer_WithValidCustomData_PersistsItVerbatim()
    {
        _definitions.Seed(RequiredString("Customer", "bloodType"));

        var result = await CreateCustomerHandler().HandleAsync(
            new CreateCustomerCommand(_companyId, "CUST-001", "ACME Corp")
            {
                CustomData = """{"bloodType": "O+"}""",
            });

        Assert.True(result.IsSuccess);
        Assert.NotNull(_customers.AddedCustomer);
        Assert.Equal("""{"bloodType": "O+"}""", _customers.AddedCustomer!.CustomData);
        Assert.Equal("""{"bloodType": "O+"}""", result.Value!.CustomData);
    }

    [Fact]
    public async Task CreateCustomer_WithWrongCustomType_FailsWithValidationCodeAndPersistsNothing()
    {
        _definitions.Seed(RequiredString("Customer", "bloodType"));

        var result = await CreateCustomerHandler().HandleAsync(
            new CreateCustomerCommand(_companyId, "CUST-001", "ACME Corp")
            {
                CustomData = """{"bloodType": 123}""",
            });

        Assert.False(result.IsSuccess);
        Assert.Equal(ExtensibilityErrorCodes.CustomFieldValidationFailed, result.Error!.Code);
        Assert.Null(_customers.AddedCustomer);
    }

    [Fact]
    public async Task CreateCustomer_MissingRequiredCustomField_Fails()
    {
        _definitions.Seed(RequiredString("Customer", "bloodType"));

        var result = await CreateCustomerHandler().HandleAsync(
            new CreateCustomerCommand(_companyId, "CUST-001", "ACME Corp"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ExtensibilityErrorCodes.CustomFieldValidationFailed, result.Error!.Code);
    }

    // ---------------------------------------------------------------- validator unit cases

    [Fact]
    public async Task Validator_UnknownKeysAreIgnored_ForwardCompatibility()
    {
        _definitions.Seed(RequiredString("Customer", "bloodType"));

        // A retired definition never breaks stored documents carrying extra keys.
        var error = await Validator().ValidateAsync(
            "Customer", """{"bloodType": "A-", "retiredField": 42}""");

        Assert.Null(error);
    }

    [Fact]
    public async Task Validator_MalformedJson_Fails()
    {
        _definitions.Seed(RequiredString("Customer", "bloodType"));

        var error = await Validator().ValidateAsync("Customer", "{not json");

        Assert.NotNull(error);
        Assert.Equal(ExtensibilityErrorCodes.CustomFieldValidationFailed, error!.Code);
    }

    [Fact]
    public async Task Validator_SelectAcceptsOnlyConfiguredOptions()
    {
        _definitions.Seed(new CustomFieldDefinition
        {
            Id = Guid.NewGuid(),
            EntityName = "Item",
            FieldName = "origin",
            FieldType = "select",
            IsRequired = true,
            IsActive = true,
            ValidationJsonSchema = """{"options": ["local", "imported"]}""",
            CreatedAt = DateTimeOffset.UtcNow,
        });

        Assert.Null(await Validator().ValidateAsync("Item", """{"origin": "local"}"""));

        var error = await Validator().ValidateAsync("Item", """{"origin": "martian"}""");
        Assert.NotNull(error);
        Assert.Equal(ExtensibilityErrorCodes.CustomFieldValidationFailed, error!.Code);
    }

    [Fact]
    public async Task Validator_DateRequiresIsoFormat()
    {
        _definitions.Seed(new CustomFieldDefinition
        {
            Id = Guid.NewGuid(),
            EntityName = "Asset",
            FieldName = "warrantyUntil",
            FieldType = "date",
            IsRequired = true,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        });

        Assert.Null(await Validator().ValidateAsync("Asset", """{"warrantyUntil": "2027-01-15"}"""));

        var error = await Validator().ValidateAsync("Asset", """{"warrantyUntil": "15/01/2027"}""");
        Assert.NotNull(error);
    }

    [Fact]
    public async Task Validator_NoDefinitions_EmptyPayloadIsValid()
    {
        Assert.Null(await Validator().ValidateAsync("Customer", null));
        Assert.Null(await Validator().ValidateAsync("Customer", "  "));
    }

    // ------------------------------------------------------- EXT-G3 / EXT-G4 (submit hooks)

    private sealed class RecordingHook(
        int order, List<string> log, string name, Exception? throwOnBefore = null)
        : IDocumentLifecycleHook<SalesInvoice>
    {
        public int Order => order;

        public Task OnBeforeSubmitAsync(SalesInvoice document, CancellationToken ct)
        {
            log.Add($"{name}:before-submit");
            return throwOnBefore is null ? Task.CompletedTask : Task.FromException(throwOnBefore);
        }

        public Task OnAfterSubmitAsync(SalesInvoice document, CancellationToken ct)
        {
            log.Add($"{name}:after-submit");
            return Task.CompletedTask;
        }

        public Task OnBeforeCancelAsync(SalesInvoice document, CancellationToken ct) => Task.CompletedTask;

        public Task OnAfterCancelAsync(SalesInvoice document, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class SubmitFixture
    {
        public FakeSalesInvoiceRepository Invoices { get; } = new();
        public FakeCustomerRepository Customers { get; } = new();
        public FakeCompanyRepository Companies { get; } = new();
        public FakeAccountRepository Accounts { get; } = new();
        public FakeWarehouseRepository Warehouses { get; } = new();
        public FakeExchangeRateRepository ExchangeRates { get; } = new();
        public FakeStockRepository Stock { get; } = new();
        public FakeItemRepository Items { get; } = new();

        public Guid CompanyId { get; } = Guid.NewGuid();
        public Customer Customer { get; }
        public SalesInvoice Invoice { get; }

        public SubmitFixture()
        {
            Companies.Company = new Company
            {
                Id = CompanyId,
                TenantId = Guid.NewGuid(),
                Name = "Acme Industrial",
                DefaultReceivableAccountCode = "1120",
                DefaultIncomeAccountCode = "4110",
            };

            Customer = new Customer
            {
                Id = Guid.NewGuid(),
                CompanyId = CompanyId,
                CustomerCode = "CUST-001",
                CustomerName = "ACME Corp",
                IsActive = true,
            };
            Customers.Seed(Customer);

            var receivable = Leaf("1120", "Accounts Receivable", AccountRootType.Asset);
            var income = Leaf("4110", "Sales Revenue", AccountRootType.Income);
            Accounts.Seed(receivable, income);
            Accounts.AccountsByCodeMap["1120"] = new[] { receivable };
            Accounts.AccountsByCodeMap["4110"] = new[] { income };

            Invoice = new SalesInvoice
            {
                Id = Guid.NewGuid(),
                CompanyId = CompanyId,
                InvoiceNumber = "SINV-2026-00001",
                CustomerId = Customer.Id,
                PostingDate = PostingDate,
                DueDate = PostingDate,
                Status = SalesInvoiceStatus.Draft,
                NetTotal = 1000m,
                GrandTotal = 1000m,
                OutstandingAmount = 0m,
                CreatedAt = DateTimeOffset.UtcNow,
                Items = new List<SalesInvoiceItem>
                {
                    new() { Id = Guid.NewGuid(), ItemId = Guid.NewGuid(), Quantity = 10m, Rate = 100m, Amount = 1000m },
                },
            };
            Invoices.Seed(Invoice);
        }

        private Account Leaf(string code, string name, AccountRootType root) => new()
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CompanyId = CompanyId,
            AccountCode = code,
            AccountName = name,
            RootType = root,
            IsGroup = false,
            IsActive = true,
        };

        public SubmitSalesInvoiceCommandHandler Handler(params IDocumentLifecycleHook<SalesInvoice>[] hooks) =>
            new(Invoices, Customers, Companies, Accounts, ExchangeRates, Stock, Warehouses, hooks);
    }

    [Fact]
    public async Task Submit_HooksRunOrdered_AroundThePosting()
    {
        var fixture = new SubmitFixture();
        var log = new List<string>();
        var handler = fixture.Handler(
            new RecordingHook(20, log, "B"),
            new RecordingHook(10, log, "A"));

        var result = await handler.HandleAsync(new SubmitSalesInvoiceCommand(fixture.CompanyId, fixture.Invoice.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal(
            ["A:before-submit", "B:before-submit", "A:after-submit", "B:after-submit"],
            log);
        Assert.Equal(2, fixture.Invoices.GlEntries.Count); // A/R + revenue posted once
    }

    [Fact]
    public async Task Submit_RejectingHook_AbortsWithHookCodeAndPostsNoLedger()
    {
        var fixture = new SubmitFixture();
        var handler = fixture.Handler(
            new RecordingHook(
                5, new List<string>(), "insurance",
                throwOnBefore: new LifecycleHookRejectedException(
                    "insurance_authorization_required",
                    "Insurance authorization is required before submitting.")));

        var result = await handler.HandleAsync(new SubmitSalesInvoiceCommand(fixture.CompanyId, fixture.Invoice.Id));

        Assert.False(result.IsSuccess);
        Assert.Equal("insurance_authorization_required", result.Error!.Code);
        Assert.Equal(SalesInvoiceStatus.Draft, fixture.Invoice.Status); // rolled back
        Assert.Empty(fixture.Invoices.GlEntries); // EXT-G3: no ledger rows posted
    }
}
