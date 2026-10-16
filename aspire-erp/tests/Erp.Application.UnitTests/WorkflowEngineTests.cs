using Erp.Application.Common;
using Erp.Application.Features.Presets.Commands;
using Erp.Application.Features.Selling.Commands;
using Erp.Application.Features.Workflow.Commands;
using Erp.Application.Services;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Operational workflows Fase 5 (spec workflow-and-presets, WF-PRESET-G1..G3): submit gated
/// by CanSubmit states through the lifecycle-hook pipeline, role-checked transitions, and
/// industry preset application seeding custom fields + workflows + terminology - through
/// the order submit handler, the transition handler and the preset service.
/// </summary>
public sealed class WorkflowEngineTests
{
    private readonly Guid _companyId = Guid.NewGuid();

    private readonly FakeCompanyRepository _companies = new();
    private readonly FakeCustomerRepository _customers = new();
    private readonly FakeItemRepository _items = new();
    private readonly FakeSalesOrderRepository _salesOrders = new();
    private readonly FakeSalesInvoiceRepository _salesInvoices = new();
    private readonly FakePurchaseRepository _purchases = new();
    private readonly FakeWorkflowRepository _workflows = new();
    private readonly FakeCustomFieldDefinitionRepository _customFields = new();
    private readonly FakeTaxTemplateRepository _taxTemplates = new();
    private readonly FakeAccountRepository _accounts = new();
    private readonly FakeTenantPresetRepository _tenantPresets = new();

    private readonly Customer _customer;
    private readonly SalesOrder _order;

    public WorkflowEngineTests()
    {
        _companies.Company = new Company
        {
            Id = _companyId,
            TenantId = Guid.NewGuid(),
            Name = "Clinica Santa Maria",
        };

        _customer = new Customer
        {
            Id = Guid.NewGuid(),
            CompanyId = _companyId,
            CustomerCode = "PAC-001",
            CustomerName = "Juan Perez",
            IsActive = true,
        };
        _customers.Seed(_customer);

        _order = new SalesOrder
        {
            Id = Guid.NewGuid(),
            CompanyId = _companyId,
            CustomerId = _customer.Id,
            Status = SalesOrderStatus.Draft,
            TransactionDate = new DateOnly(2026, 4, 1),
            DeliveryDate = new DateOnly(2026, 4, 8),
            OrderNumber = "SO-2026-00001",
            GrandTotal = 0m,
        };
        _salesOrders.SeedOrder(_order);
    }

    private sealed class TestRoles : ICurrentUserRoles
    {
        public TestRoles(params string[] roles)
        {
            Roles = roles;
        }

        public IReadOnlyCollection<string> Roles { get; }
    }

    /// <summary>Healthcare-like SalesOrder flow (spec background: Admision/Consulta/Autorizado).</summary>
    private static WorkflowDefinition ClinicFlow() => new()
    {
        Id = Guid.NewGuid(),
        DocumentType = nameof(SalesOrder),
        Name = "Atención clínica",
        IsActive = true,
        CreatedAt = DateTimeOffset.UtcNow,
        States = new List<WorkflowStateDefinition>
        {
            new() { Id = Guid.NewGuid(), StateName = "Admision", IsInitial = true, CanSubmit = false },
            new() { Id = Guid.NewGuid(), StateName = "Consulta", IsInitial = false, CanSubmit = false },
            new() { Id = Guid.NewGuid(), StateName = "Autorizado", IsInitial = false, CanSubmit = true },
        },
        Transitions = new List<WorkflowTransition>
        {
            new() { Id = Guid.NewGuid(), FromState = "Admision", ToState = "Consulta", ActionLabel = "Iniciar consulta", AllowedRole = "Recepcion" },
            new() { Id = Guid.NewGuid(), FromState = "Consulta", ToState = "Autorizado", ActionLabel = "Autorizar", AllowedRole = "Medico" },
        },
    };

    private SubmitSalesOrderCommandHandler SubmitHandler() =>
        new(_customers, _items, _salesOrders,
            new IDocumentLifecycleHook<SalesOrder>[] { new WorkflowSubmitGuard<SalesOrder>(_workflows) });

    private ApplyWorkflowTransitionCommandHandler TransitionHandler(ICurrentUserRoles? roles) =>
        new(_workflows, _salesOrders, _salesInvoices, _purchases, roles);

    // ------------------------------------------------------- WF-PRESET-G1 (submit blocked)

    [Fact]
    public async Task Submit_InInitialState_BlockedWithSubmitNotAllowed()
    {
        _workflows.Seed(ClinicFlow());

        // Null operational state adopts the initial ("Admision", CanSubmit=false).
        var result = await SubmitHandler().HandleAsync(new SubmitSalesOrderCommand(_companyId, _order.Id));

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkflowErrorCodes.WorkflowSubmitNotAllowed, result.Error!.Code);
        Assert.Equal(SalesOrderStatus.Draft, _order.Status); // untouched, zero writes
        Assert.Equal("Admision", _order.WorkflowState); // initial adopted
    }

    [Fact]
    public async Task Submit_WithoutFlow_PassesUnchanged()
    {
        var result = await SubmitHandler().HandleAsync(new SubmitSalesOrderCommand(_companyId, _order.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal(SalesOrderStatus.Submitted, _order.Status);
    }

    // ------------------------------------------------------- WF-PRESET-G2 (happy path)

    [Fact]
    public async Task AuthorizedTransitionsThenSubmit_Succeeds()
    {
        _workflows.Seed(ClinicFlow());

        var toConsulta = await TransitionHandler(new TestRoles("Recepcion")).HandleAsync(
            new ApplyWorkflowTransitionCommand(nameof(SalesOrder), _companyId, _order.Id, "Consulta"));
        Assert.True(toConsulta.IsSuccess);
        Assert.Equal("Consulta", _order.WorkflowState);

        var toAutorizado = await TransitionHandler(new TestRoles("Medico")).HandleAsync(
            new ApplyWorkflowTransitionCommand(nameof(SalesOrder), _companyId, _order.Id, "Autorizado"));
        Assert.True(toAutorizado.IsSuccess);

        var submitted = await SubmitHandler().HandleAsync(new SubmitSalesOrderCommand(_companyId, _order.Id));
        Assert.True(submitted.IsSuccess);
        Assert.Equal(SalesOrderStatus.Submitted, _order.Status);
        Assert.Equal("Autorizado", submitted.Value!.WorkflowState);
    }

    [Fact]
    public async Task Transition_WrongRole_RejectedWithRoleUnauthorized()
    {
        _workflows.Seed(ClinicFlow());

        var result = await TransitionHandler(new TestRoles("Medico")).HandleAsync(
            new ApplyWorkflowTransitionCommand(nameof(SalesOrder), _companyId, _order.Id, "Consulta"));

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkflowErrorCodes.WorkflowRoleUnauthorized, result.Error!.Code);
        Assert.Null(_order.WorkflowState);
    }

    [Fact]
    public async Task Transition_NoEdge_RejectedWithInvalidTransition()
    {
        _workflows.Seed(ClinicFlow());

        // Admision -> Autorizado skips Consulta: no such edge.
        var result = await TransitionHandler(new TestRoles("Administracion")).HandleAsync(
            new ApplyWorkflowTransitionCommand(nameof(SalesOrder), _companyId, _order.Id, "Autorizado"));

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkflowErrorCodes.InvalidWorkflowTransition, result.Error!.Code);
    }

    // ------------------------------------------------------------------ definition CRUD

    [Fact]
    public async Task CreateWorkflow_TwoInitials_Rejected()
    {
        var handler = new CreateWorkflowDefinitionCommandHandler(_workflows);

        var result = await handler.HandleAsync(new CreateWorkflowDefinitionCommand(
            nameof(SalesOrder),
            "Bad flow",
            true,
            [new WorkflowStateInput("A", true, false), new WorkflowStateInput("B", true, true)],
            [new WorkflowTransitionInput("A", "B", "Go", "")]));

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkflowErrorCodes.InvalidWorkflowTransition, result.Error!.Code);
    }

    [Fact]
    public async Task CreateWorkflow_UnknownEndpoint_Rejected()
    {
        var handler = new CreateWorkflowDefinitionCommandHandler(_workflows);

        var result = await handler.HandleAsync(new CreateWorkflowDefinitionCommand(
            nameof(SalesOrder),
            "Bad flow",
            true,
            [new WorkflowStateInput("A", true, false)],
            [new WorkflowTransitionInput("A", "Nowhere", "Go", "")]));

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkflowErrorCodes.InvalidWorkflowTransition, result.Error!.Code);
    }

    // ------------------------------------------------------- WF-PRESET-G3 (preset apply)

    [Fact]
    public async Task ApplyHealthcarePreset_SeedsFieldsWorkflowsAndTerminology()
    {
        var service = new VerticalPresetService(
            _customFields, _workflows, _taxTemplates, _accounts, _tenantPresets);

        var result = await service.ApplyAsync("healthcare", _companyId);

        // Gherkin: bloodType + insurancePolicy seeded for Customer...
        Assert.Equal(2, result.CustomFieldsCreated);
        Assert.Equal(0, result.CustomFieldsSkipped);
        var fields = await _customFields.GetActiveByEntityAsync("Customer");
        Assert.Contains(fields, f => f.FieldName == "bloodType" && f.IsRequired);
        Assert.Contains(fields, f => f.FieldName == "insurancePolicy");

        // ...the SalesOrder flow seeded and active...
        Assert.Equal(1, result.WorkflowsCreated);
        var flow = await _workflows.GetActiveByDocumentTypeAsync(nameof(SalesOrder));
        Assert.NotNull(flow);
        Assert.Equal(3, flow!.States.Count);
        Assert.Equal(2, flow.Transitions.Count);

        // ...tax templates skipped (no chart accounts in the test tenant)...
        Assert.Equal(0, result.TaxTemplatesCreated);
        Assert.Equal(1, result.TaxTemplatesSkipped);

        // ...and terminology served for the overlay (Gherkin: Paciente mapping).
        var active = await service.GetActiveAsync();
        Assert.Equal("healthcare", active.PresetKey);
        Assert.True(active.Terminology.TryGetValue("es", out var es));
        Assert.True(es.TryGetValue("selling:customerForm.titleNew", out var title));
        Assert.Equal("Nuevo Paciente", title);
    }

    [Fact]
    public async Task ApplyPreset_UnknownKey_RejectedWithPresetNotFound()
    {
        var service = new VerticalPresetService(
            _customFields, _workflows, _taxTemplates, _accounts, _tenantPresets);

        var ex = await Assert.ThrowsAsync<WorkflowValidationException>(
            () => service.ApplyAsync("no-such-vertical", _companyId));
        Assert.Equal(WorkflowErrorCodes.PresetNotFound, ex.Code);
    }

    [Fact]
    public async Task ApplyPresetTwice_IsIdempotent()
    {
        var service = new VerticalPresetService(
            _customFields, _workflows, _taxTemplates, _accounts, _tenantPresets);

        var first = await service.ApplyAsync("healthcare", _companyId);
        Assert.Equal(2, first.CustomFieldsCreated);

        var second = await service.ApplyAsync("healthcare", _companyId);
        Assert.Equal(0, second.CustomFieldsCreated);
        Assert.Equal(2, second.CustomFieldsSkipped);
        Assert.Equal(0, second.WorkflowsCreated);
        Assert.Equal(1, second.WorkflowsSkipped);
    }
}
