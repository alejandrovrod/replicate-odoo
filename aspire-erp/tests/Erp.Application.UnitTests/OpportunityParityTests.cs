using Erp.Application.Features.Crm.Commands;
using Erp.Application.Features.Crm.Queries;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Xunit;

namespace Erp.Application.UnitTests.Crm;

/// <summary>
/// CRM/ERPNext parity suites: the detail dossier read, the dossier edit (lines replace,
/// segmentation, competitors, currency) and the ClosedWon quotation issue - exercised
/// through the new handlers over in-memory fakes.
/// </summary>
public sealed class OpportunityParityTests
{
    private static readonly DateOnly Today = new(2026, 10, 16);

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    private readonly FakeCrmParityRepository _crm = new();
    private readonly FakeCrmCatalogRepository _catalogs = new();
    private readonly FakeQuotationRepository _quotations = new();
    private readonly FakeItemRepository _items = new();
    private readonly FakeCompanyRepository _companies = new();
    private readonly FakeCurrencyRepository _currencies = new();
    private readonly FakeExchangeRateRepository _exchangeRates = new();
    private readonly FakeCustomerRepository _customers = new();
    private readonly FakeCrmActivityRepository _activities = new();

    private static readonly byte[] RowV1 = [1, 2, 3, 4];

    private readonly Item _item;
    private readonly Customer _customer;
    private readonly Currency _usd;
    private readonly Currency _eur;

    public OpportunityParityTests()
    {
        _companies.Company = new Company
        {
            Id = _companyId,
            TenantId = _tenantId,
            Name = "Acme",
        };

        _item = new Item
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            CompanyId = _companyId,
            ItemCode = "IT-001",
            ItemName = "Steel Bracket",
            StockUomId = Guid.NewGuid(),
        };
        _items.Seed(_item);

        _customer = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            CompanyId = _companyId,
            CustomerCode = "CUST-001",
            CustomerName = "ACME Corp",
            IsActive = true,
        };
        _customers.Seed(_customer);

        _usd = new Currency { Id = Guid.NewGuid(), Code = "USD", Symbol = "$" };
        _eur = new Currency { Id = Guid.NewGuid(), Code = "EUR", Symbol = "€" };
        _currencies.Seed(_usd, _eur);

        _catalogs.SeedReasons(new OpportunityLossReason
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Code = "PRICE", Name = "Lost on Price",
        });
        _catalogs.SeedTerritories(new Territory
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Code = "NORTH", Name = "North",
        });
        _catalogs.SeedIndustries(new Industry
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Code = "RETAIL", Name = "Retail",
        });
    }

    private Opportunity OpenDeal(
        decimal amount = 1000m,
        string stage = OpportunityStage.Negotiation,
        Guid? customerId = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            CompanyId = _companyId,
            OpportunityNumber = "OPP-2026-00042",
            OpportunityFrom = "Customer",
            PartyId = customerId ?? _customer.Id,
            PartyName = "ACME Corp",
            Stage = stage,
            Status = OpportunityStatus.Open,
            OpportunityAmount = amount,
            BaseOpportunityAmount = amount,
            Probability = 80m,
            ExpectedClosingDate = Today.AddDays(30),
            RowVersion = (byte[])RowV1.Clone(),
        };

    private GetOpportunityDetailQueryHandler DetailHandler() =>
        new(_crm, _catalogs, _currencies);

    private UpdateOpportunityCommandHandler UpdateHandler() =>
        new(_crm, _catalogs, _items, _companies, _currencies, _exchangeRates, DetailHandler());

    private CreateQuotationFromOpportunityCommandHandler QuotationHandler() =>
        new(_crm, _quotations, _activities, _customers, _items, _currencies);

    // ------------------------------------------------------------------ detail

    [Fact]
    public async Task Detail_OpenDeal_ReturnsDossierWithLinesAndHistory()
    {
        var deal = OpenDeal();
        deal.Items.Add(new OpportunityItem
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            OpportunityId = deal.Id,
            ItemId = _item.Id,
            ItemName = _item.ItemName,
            Quantity = 2m,
            Rate = 100m,
            DiscountPercentage = 10m,
        });
        _crm.SeedOpportunity(deal);

        var detail = await DetailHandler().HandleAsync(
            new GetOpportunityDetailQuery(_companyId, deal.Id));

        Assert.NotNull(detail);
        Assert.Equal("OPP-2026-00042", detail!.OpportunityNumber);
        Assert.Equal("USD", detail.CurrencyCode);
        var line = Assert.Single(detail.Items);
        Assert.Equal(180m, line.Amount);
        Assert.Empty(detail.Activities);
        Assert.Empty(detail.Quotations);
    }

    [Fact]
    public async Task Detail_UnknownDeal_ReturnsNull()
    {
        var detail = await DetailHandler().HandleAsync(
            new GetOpportunityDetailQuery(_companyId, Guid.NewGuid()));

        Assert.Null(detail);
    }

    [Fact]
    public async Task Detail_ForeignCompanyDeal_ReturnsNull()
    {
        var deal = OpenDeal();
        _crm.SeedOpportunity(deal);

        var detail = await DetailHandler().HandleAsync(
            new GetOpportunityDetailQuery(Guid.NewGuid(), deal.Id));

        Assert.Null(detail);
    }

    // ------------------------------------------------------------------ update

    [Fact]
    public async Task Update_LinesAndSegmentation_RecalculatesAndPersists()
    {
        var deal = OpenDeal();
        var territory = (await _catalogs.ListTerritoriesAsync()).Single();
        var industry = (await _catalogs.ListIndustriesAsync()).Single();
        _crm.SeedOpportunity(deal);

        var result = await UpdateHandler().HandleAsync(new UpdateOpportunityCommand(
            deal.Id,
            _companyId,
            (byte[])RowV1.Clone(),
            Probability: 90m,
            TerritoryId: territory.Id,
            IndustryId: industry.Id,
            MarketSegment: "SMB",
            Competitors: new[] { "Globex", "globex ", "Initech" },
            Items: new[]
            {
                new OpportunityItemInput(_item.Id, Quantity: 3m, Rate: 100m, DiscountPercentage: 0m),
            }));

        Assert.True(result.IsSuccess);
        // 3 × 100 = 300, competitors de-duplicated case-insensitively and trimmed.
        Assert.Equal(300m, result.Value!.OpportunityAmount);
        Assert.Equal(300m, result.Value.BaseOpportunityAmount);
        Assert.Equal(90m, result.Value.Probability);
        Assert.Equal("North", result.Value.TerritoryName);
        Assert.Equal("Retail", result.Value.IndustryName);
        Assert.Equal("SMB", result.Value.MarketSegment);
        Assert.Equal(new[] { "Globex", "Initech" }, result.Value.Competitors);
        Assert.Equal(300m, deal.OpportunityAmount);
        Assert.Single(deal.Items);
    }

    [Fact]
    public async Task Update_ForeignCurrency_ResolvesLiveRateForBase()
    {
        _companies.Company!.CurrencyId = _usd.Id;
        _exchangeRates.Rates.Add(new ExchangeRate
        {
            Id = Guid.NewGuid(),
            FromCurrencyId = _eur.Id,
            ToCurrencyId = _usd.Id,
            RateDate = Today,
            Rate = 1.10m,
        });
        var deal = OpenDeal();
        _crm.SeedOpportunity(deal);

        var result = await UpdateHandler().HandleAsync(new UpdateOpportunityCommand(
            deal.Id,
            _companyId,
            (byte[])RowV1.Clone(),
            CurrencyId: _eur.Id,
            Items: new[]
            {
                new OpportunityItemInput(_item.Id, Quantity: 2m, Rate: 100m),
            }));

        Assert.True(result.IsSuccess);
        Assert.Equal("EUR", result.Value!.CurrencyCode);
        Assert.Equal(1.10m, deal.ExchangeRate);
        Assert.Equal(220m, result.Value.BaseOpportunityAmount);
    }

    [Fact]
    public async Task Update_UnknownItem_FailsWithInvalidItem()
    {
        var deal = OpenDeal();
        _crm.SeedOpportunity(deal);

        var result = await UpdateHandler().HandleAsync(new UpdateOpportunityCommand(
            deal.Id,
            _companyId,
            (byte[])RowV1.Clone(),
            Items: new[] { new OpportunityItemInput(Guid.NewGuid(), Quantity: 1m, Rate: 10m) }));

        Assert.False(result.IsSuccess);
        Assert.Equal(CRMErrorCodes.InvalidOpportunityItem, result.Error!.Code);
    }

    [Fact]
    public async Task Update_BadLineMath_FailsWithInvalidItem()
    {
        var deal = OpenDeal();
        _crm.SeedOpportunity(deal);

        var result = await UpdateHandler().HandleAsync(new UpdateOpportunityCommand(
            deal.Id,
            _companyId,
            (byte[])RowV1.Clone(),
            Items: new[] { new OpportunityItemInput(_item.Id, Quantity: 0m, Rate: 10m) }));

        Assert.False(result.IsSuccess);
        Assert.Equal(CRMErrorCodes.InvalidOpportunityItem, result.Error!.Code);
    }

    [Fact]
    public async Task Update_ClosedDeal_FailsWithNotOpen()
    {
        var deal = OpenDeal();
        deal.Status = OpportunityStatus.Won;
        deal.Stage = OpportunityStage.ClosedWon;
        _crm.SeedOpportunity(deal);

        var result = await UpdateHandler().HandleAsync(new UpdateOpportunityCommand(
            deal.Id, _companyId, (byte[])RowV1.Clone(), Probability: 10m));

        Assert.False(result.IsSuccess);
        Assert.Equal(CRMErrorCodes.OpportunityNotOpen, result.Error!.Code);
    }

    [Fact]
    public async Task Update_StaleRowVersion_FailsWithConcurrencyConflict()
    {
        var deal = OpenDeal();
        _crm.SeedOpportunity(deal);

        var result = await UpdateHandler().HandleAsync(new UpdateOpportunityCommand(
            deal.Id, _companyId, [9, 9, 9, 9], Probability: 10m));

        Assert.False(result.IsSuccess);
        Assert.Equal("concurrency_conflict", result.Error!.Code);
    }

    // ------------------------------------------------------------------ quotation

    [Fact]
    public async Task Quotation_FromWonDealWithLines_DragsLinesAndNumbers()
    {
        var deal = OpenDeal();
        deal.Status = OpportunityStatus.Won;
        deal.Stage = OpportunityStage.ClosedWon;
        deal.Items.Add(new OpportunityItem
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            OpportunityId = deal.Id,
            ItemId = _item.Id,
            ItemName = _item.ItemName,
            Quantity = 2m,
            Rate = 100m,
            DiscountPercentage = 0m,
        });
        _crm.SeedOpportunity(deal);

        var result = await QuotationHandler().HandleAsync(
            new CreateQuotationFromOpportunityCommand(deal.Id, _companyId));

        Assert.True(result.IsSuccess);
        var quotation = result.Value!;
        Assert.Matches(@"^QTN-\d{4}-\d{5}$", quotation.QuotationNo);
        Assert.Equal(deal.Id, quotation.OpportunityId);
        Assert.Equal(_customer.Id, quotation.CustomerId);
        Assert.Equal(200m, quotation.TotalAmount);
        var line = Assert.Single(quotation.Items);
        Assert.Equal(_item.Id, line.ItemId);
        Assert.Equal("Steel Bracket", line.ItemName);
        Assert.Equal(Today.AddDays(30), quotation.ValidTill);
    }

    [Fact]
    public async Task Quotation_FromOpenDeal_FailsWithNotWon()
    {
        var deal = OpenDeal();
        _crm.SeedOpportunity(deal);

        var result = await QuotationHandler().HandleAsync(
            new CreateQuotationFromOpportunityCommand(deal.Id, _companyId));

        Assert.False(result.IsSuccess);
        Assert.Equal(CRMErrorCodes.OpportunityNotWon, result.Error!.Code);
        Assert.Empty(_quotations.Quotations);
    }

    [Fact]
    public async Task Quotation_AmountOnlyDealWithExplicitLines_UsesExplicitLines()
    {
        var deal = OpenDeal();
        deal.Status = OpportunityStatus.Won;
        deal.Stage = OpportunityStage.ClosedWon;
        _crm.SeedOpportunity(deal);

        var result = await QuotationHandler().HandleAsync(
            new CreateQuotationFromOpportunityCommand(
                deal.Id, _companyId,
                Lines: new[] { new QuotationLineInput(_item.Id, Quantity: 1m, Rate: 50m) }));

        Assert.True(result.IsSuccess);
        Assert.Equal(50m, result.Value!.TotalAmount);
    }

    [Fact]
    public async Task Quotation_AmountOnlyDealWithoutLines_FailsWithRequiresItems()
    {
        var deal = OpenDeal();
        deal.Status = OpportunityStatus.Won;
        deal.Stage = OpportunityStage.ClosedWon;
        _crm.SeedOpportunity(deal);

        var result = await QuotationHandler().HandleAsync(
            new CreateQuotationFromOpportunityCommand(deal.Id, _companyId));

        Assert.False(result.IsSuccess);
        Assert.Equal(CRMErrorCodes.QuotationRequiresItems, result.Error!.Code);
    }

    [Fact]
    public async Task Quotation_DealWithoutCustomer_FailsWithNoCustomer()
    {
        var deal = OpenDeal(customerId: Guid.NewGuid());
        deal.Status = OpportunityStatus.Won;
        deal.Stage = OpportunityStage.ClosedWon;
        deal.Items.Add(new OpportunityItem
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            OpportunityId = deal.Id,
            ItemId = _item.Id,
            ItemName = _item.ItemName,
            Quantity = 1m,
            Rate = 10m,
            DiscountPercentage = 0m,
        });
        _crm.SeedOpportunity(deal);

        var result = await QuotationHandler().HandleAsync(
            new CreateQuotationFromOpportunityCommand(deal.Id, _companyId));

        Assert.False(result.IsSuccess);
        Assert.Equal(CRMErrorCodes.OpportunityNoCustomer, result.Error!.Code);
    }
}
