using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.Crm.Commands;
using Erp.Application.Features.Selling.Commands;
using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Erp.Application.UnitTests.Crm;

/// <summary>
/// Fix-pass C2 tests for the opportunity-to-sales-order command (spec CRM-02 1-click
/// creation): only ClosedWon deals convert, the customer resolves from the deal's own
/// linkage (direct customer first, converted lead's customer second), lead-less deals
/// without a customer are rejected loudly, and the order itself is built by the EXISTING
/// selling creation path (this handler owns only the CRM preconditions).
/// </summary>
public class CreateOpportunitySalesOrderCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_ShouldCreateOrder_WhenWonDealLinksDirectCustomer()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var oppId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var opp = WonOpportunity(companyId, oppId, customerId);
        var customer = new Customer { Id = customerId, CompanyId = companyId, CustomerCode = "C-01", CustomerName = "Buyer" };
        var sales = new FakeCreateSalesOrder(SalesOrderDtoFor(companyId, customerId));
        var activityRepo = new FakeActivityRepository();
        var handler = new CreateOpportunitySalesOrderCommandHandler(
            new FakeCrmRepository { OpportunityToReturn = opp },
            activityRepo,
            new FakeCustomerRepository { CustomerToReturn = customer },
            sales);

        // Act
        var result = await handler.HandleAsync(
            new CreateOpportunitySalesOrderCommand(companyId, oppId, itemId, 1m, 15000m));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("SO-2026-00001", result.Value!.OrderNumber);
        Assert.NotNull(sales.Received);
        Assert.Equal(customerId, sales.Received!.CustomerId);
        var line = Assert.Single(sales.Received!.Lines!);
        Assert.Equal((itemId, 1m, 15000m), (line.ItemId, line.Quantity, line.Rate));
    }

    [Fact]
    public async Task HandleAsync_ShouldResolveConvertedCustomer_WhenDealCameFromLead()
    {
        // Arrange: converted deals carry PartyId = Lead.Id (ConvertLead writes the lead, not
        // the customer), so resolution falls back to the source lead's ConvertedCustomerId.
        var companyId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var oppId = Guid.NewGuid();

        var opp = WonOpportunity(companyId, oppId, leadId);
        var lead = new Lead
        {
            Id = leadId,
            CompanyId = companyId,
            Status = LeadStatus.Converted,
            LeadCode = "L-01",
            LeadName = "Test Lead",
            ConvertedCustomerId = customerId,
        };
        var customer = new Customer { Id = customerId, CompanyId = companyId, CustomerCode = "C-01", CustomerName = "Buyer" };
        var sales = new FakeCreateSalesOrder(SalesOrderDtoFor(companyId, customerId));
        var handler = new CreateOpportunitySalesOrderCommandHandler(
            new FakeCrmRepository { OpportunityToReturn = opp, LeadToReturn = lead },
            new FakeActivityRepository(),
            new FakeCustomerRepository { CustomerToReturn = customer },
            sales);

        // Act
        var result = await handler.HandleAsync(
            new CreateOpportunitySalesOrderCommand(companyId, oppId, Guid.NewGuid(), 2m, 7500m));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(sales.Received);
        Assert.Equal(customerId, sales.Received!.CustomerId);
    }

    [Fact]
    public async Task HandleAsync_ShouldWriteLinkageNote_WhenAuthorKnown()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var oppId = Guid.NewGuid();
        var authorId = Guid.NewGuid();

        var opp = WonOpportunity(companyId, oppId, customerId);
        opp.AssignedSalespersonId = authorId;
        var customer = new Customer { Id = customerId, CompanyId = companyId, CustomerCode = "C-01", CustomerName = "Buyer" };
        var activityRepo = new FakeActivityRepository();
        var handler = new CreateOpportunitySalesOrderCommandHandler(
            new FakeCrmRepository { OpportunityToReturn = opp },
            activityRepo,
            new FakeCustomerRepository { CustomerToReturn = customer },
            new FakeCreateSalesOrder(SalesOrderDtoFor(companyId, customerId)));

        // Act
        var result = await handler.HandleAsync(
            new CreateOpportunitySalesOrderCommand(companyId, oppId, Guid.NewGuid(), 1m, 15000m));

        // Assert
        Assert.True(result.IsSuccess);
        var note = Assert.Single(activityRepo.Activities);
        Assert.Equal(oppId, note.OpportunityId);
        Assert.Contains("SO-2026-00001", note.Subject);
        Assert.Equal(authorId, note.CreatedByUserId);
    }

    [Fact]
    public async Task HandleAsync_ShouldRejectLoudly_WhenDealHasNoCustomer()
    {
        // Arrange: a lead-sourced deal whose lead was never converted (no customer anywhere).
        var companyId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var oppId = Guid.NewGuid();

        var opp = WonOpportunity(companyId, oppId, leadId);
        var lead = new Lead
        {
            Id = leadId,
            CompanyId = companyId,
            Status = LeadStatus.Open,
            LeadCode = "L-01",
            LeadName = "Test Lead",
        };
        var sales = new FakeCreateSalesOrder(SalesOrderDtoFor(companyId, Guid.NewGuid()));
        var handler = new CreateOpportunitySalesOrderCommandHandler(
            new FakeCrmRepository { OpportunityToReturn = opp, LeadToReturn = lead },
            new FakeActivityRepository(),
            new FakeCustomerRepository(),
            sales);

        // Act
        var result = await handler.HandleAsync(
            new CreateOpportunitySalesOrderCommand(companyId, oppId, Guid.NewGuid(), 1m, 15000m));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(CRMErrorCodes.OpportunityNoCustomer, result.Error!.Code);
        Assert.Null(sales.Received);
    }

    [Fact]
    public async Task HandleAsync_ShouldRejectNonWonDeal_WithZeroWrites()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        var oppId = Guid.NewGuid();

        var opp = WonOpportunity(companyId, oppId, Guid.NewGuid());
        opp.Status = OpportunityStatus.Open;
        opp.Stage = OpportunityStage.Negotiation;
        var sales = new FakeCreateSalesOrder(SalesOrderDtoFor(companyId, Guid.NewGuid()));
        var activityRepo = new FakeActivityRepository();
        var handler = new CreateOpportunitySalesOrderCommandHandler(
            new FakeCrmRepository { OpportunityToReturn = opp },
            activityRepo,
            new FakeCustomerRepository(),
            sales);

        // Act
        var result = await handler.HandleAsync(
            new CreateOpportunitySalesOrderCommand(companyId, oppId, Guid.NewGuid(), 1m, 15000m));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(CRMErrorCodes.OpportunityNotWon, result.Error!.Code);
        Assert.Null(sales.Received);
        Assert.Empty(activityRepo.Activities);
    }

    [Fact]
    public async Task HandleAsync_ShouldPropagateSellingFailure()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var oppId = Guid.NewGuid();

        var opp = WonOpportunity(companyId, oppId, customerId);
        var customer = new Customer { Id = customerId, CompanyId = companyId, CustomerCode = "C-01", CustomerName = "Buyer" };
        var sales = new FakeCreateSalesOrder(null);
        var handler = new CreateOpportunitySalesOrderCommandHandler(
            new FakeCrmRepository { OpportunityToReturn = opp },
            new FakeActivityRepository(),
            new FakeCustomerRepository { CustomerToReturn = customer },
            sales);

        // Act: zero quantity fails the selling line rule inside the EXISTING handler.
        var result = await handler.HandleAsync(
            new CreateOpportunitySalesOrderCommand(companyId, oppId, Guid.NewGuid(), 0m, 15000m));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_quantity", result.Error!.Code);
    }

    private static Opportunity WonOpportunity(Guid companyId, Guid oppId, Guid partyId) =>
        new()
        {
            Id = oppId,
            CompanyId = companyId,
            OpportunityNumber = "OPP-2026-00042",
            OpportunityFrom = "Lead",
            PartyId = partyId,
            PartyName = "TechCorp",
            Stage = OpportunityStage.ClosedWon,
            Status = OpportunityStatus.Won,
            OpportunityAmount = 15000m,
            Probability = 100m,
            Currency = "USD",
            RowVersion = new byte[] { 1, 2, 3, 4 },
        };

    private static SalesOrderDto SalesOrderDtoFor(Guid companyId, Guid customerId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return new SalesOrderDto(
            Guid.NewGuid(), companyId, customerId, "C-01", "Buyer",
            SalesOrderStatus.Draft, today, today.AddDays(30), "SO-2026-00001",
            15000m, 0m, 15000m, 0m, 0m, DateTimeOffset.UtcNow,
            Array.Empty<SalesOrderLineDto>());
    }

    private sealed class FakeCrmRepository : ICrmRepository
    {
        public Opportunity? OpportunityToReturn { get; set; }
        public Lead? LeadToReturn { get; set; }

        public Task<Lead?> GetLeadByIdAsync(Guid leadId, CancellationToken cancellationToken = default) => Task.FromResult(LeadToReturn);
        public Task<Opportunity?> GetOpportunityByIdAsync(Guid opportunityId, CancellationToken cancellationToken = default) => Task.FromResult(OpportunityToReturn);
        public Task AddLeadAsync(Lead lead, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateLeadAsync(Lead lead, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddOpportunityAsync(Opportunity opportunity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateOpportunityAsync(Opportunity opportunity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<string> NextOpportunityNumberAsync(Guid companyId, int year, CancellationToken cancellationToken = default) => Task.FromResult("OPP-1");
        public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default) => operation(cancellationToken);
        public Task<Lead?> GetLeadByDedupKeyAsync(Guid companyId, string source, string externalReference, CancellationToken cancellationToken = default) => Task.FromResult<Lead?>(null);
        public Task<PagedResult<Lead>> ListLeadsAsync(Guid companyId, PagedRequest paging, CancellationToken cancellationToken = default) => Task.FromResult(new PagedResult<Lead>(new List<Lead>(), 0, paging.SafePageNumber, paging.SafePageSize));
        public Task<PagedResult<Opportunity>> ListOpportunitiesAsync(Guid companyId, PagedRequest paging, string? stage, CancellationToken cancellationToken = default) => Task.FromResult(new PagedResult<Opportunity>(new List<Opportunity>(), 0, paging.SafePageNumber, paging.SafePageSize));
    }

    private sealed class FakeCustomerRepository : ICustomerRepository
    {
        public Customer? CustomerToReturn { get; set; }

        public Task AddAsync(Customer customer, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> ExistsCodeAsync(Guid companyId, string customerCode, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<Customer?> GetByIdAsync(Guid customerId, CancellationToken cancellationToken = default) => Task.FromResult(CustomerToReturn);
        public Task<PagedResult<Customer>> GetRecentAsync(Guid companyId, PagedRequest paging, CancellationToken cancellationToken = default) => Task.FromResult(new PagedResult<Customer>(new List<Customer>(), 0, paging.SafePageNumber, paging.SafePageSize));
        public Task UpdateAsync(Customer customer, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeActivityRepository : ICrmActivityRepository
    {
        public System.Collections.Generic.List<CRMActivity> Activities { get; } = new();
        public Task AddActivityAsync(CRMActivity activity, CancellationToken cancellationToken = default) { Activities.Add(activity); return Task.CompletedTask; }
        public Task<System.Collections.Generic.IReadOnlyList<CRMActivity>> ListByOpportunityAsync(Guid opportunityId, CancellationToken cancellationToken = default) => Task.FromResult<System.Collections.Generic.IReadOnlyList<CRMActivity>>(Activities.FindAll(a => a.OpportunityId == opportunityId));
    }

    private sealed class FakeCreateSalesOrder : ICommandHandler<CreateSalesOrderCommand, Result<SalesOrderDto>>
    {
        private readonly SalesOrderDto? _orderToReturn;

        public CreateSalesOrderCommand? Received { get; private set; }

        public FakeCreateSalesOrder(SalesOrderDto? orderToReturn) => _orderToReturn = orderToReturn;

        public Task<Result<SalesOrderDto>> HandleAsync(CreateSalesOrderCommand command, CancellationToken cancellationToken = default)
        {
            Received = command;

            if (_orderToReturn is not null)
            {
                return Task.FromResult(Result<SalesOrderDto>.Success(_orderToReturn));
            }

            return Task.FromResult(Result<SalesOrderDto>.Failure(
                "invalid_quantity", "Line quantity must be greater than zero (received 0)."));
        }
    }
}
