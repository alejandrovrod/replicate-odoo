using System.Text.Json.Serialization;
using Erp.Api.Authorization;
using Erp.Api.Filters;
using Erp.Api.Middleware;
using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.Accounts.Commands;
using Erp.Application.Features.Accounts.Queries;
using Erp.Application.Features.Banking.Commands;
using Erp.Application.Features.Banking.Parsers;
using Erp.Application.Features.Banking.Queries;
using Erp.Application.Features.Buying.Commands;
using Erp.Application.Features.Buying.Queries;
using Erp.Application.Features.GeneralLedger.Commands;
using Erp.Application.Features.GeneralLedger.Queries;
using Erp.Application.Features.Items.Commands;
using Erp.Application.Features.Items.Queries;
using Erp.Application.Features.Selling.Commands;
using Erp.Application.Features.Selling.Queries;
using Erp.Application.Features.Stock.Commands;
using Erp.Application.Features.Stock.Queries;
using Erp.Application.Features.Manufacturing.Commands;
using Erp.Application.Features.Manufacturing.Queries;
using Erp.Application.Features.Assets.Commands;
using Erp.Application.Features.Assets.Queries;
using Erp.Application.Features.Crm.Commands;
using Erp.Application.Features.Crm.DTOs;
using Erp.Application.Features.Crm.Queries;
using Erp.Application.Features.HrPayroll.Commands;
using Erp.Application.Features.HrPayroll.Queries;
using Erp.Application.Features.Warehouses.Commands;
using Erp.Application.Features.Warehouses.Queries;
using Erp.Application.Services;
using Erp.Domain.Repositories;
using Erp.Infrastructure.Data;
using Erp.Infrastructure.Data.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Aspire service defaults: OpenTelemetry export, health checks, service discovery,
// and HTTP resilience pipelines (Constitution Article V.3).
builder.AddServiceDefaults();

// Add services to the container.
// Enums serialize as their NVARCHAR names ("Asset", ...) per plan.md §7.3 so the API contract
// matches the stored value and the ubiquitous language.
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Multi-tenancy composition root (Constitution Article II.2): the scoped tenant provider is
// injected into AppDbContext, which is wired to the Aspire-injected ConnectionStrings:erp-db.
builder.Services.AddScoped<ITenantProvider, TenantProvider>();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("erp-db")
            ?? throw new InvalidOperationException(
                "Connection string 'erp-db' is missing. Run the app through Erp.AppHost "
                + "(sql.AddDatabase(\"erp-db\")) or set ConnectionStrings__erp-db.")));

// Hand-rolled CQRS (decision C2): ISender dispatches to the feature handlers below. No MediatR,
// no extra NuGet packages - Erp.Application still depends only on Erp.Domain (Constitution I.3).
builder.Services.AddScoped<ISender, Sender>();
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<ICommandHandler<CreateAccountCommand, Result<AccountDto>>, CreateAccountCommandHandler>();
builder.Services.AddScoped<IQueryHandler<GetAccountTreeQuery, IReadOnlyList<AccountTreeNodeDto>>, GetAccountTreeQueryHandler>();

// Stock & Inventory (Tasks 3.1-3.3): masters, the perpetual-inventory posting engine and the
// Article VI.4 idempotency filter. Repositories stay in Erp.Infrastructure, handlers in
// Erp.Application - only the composition root knows both (decision C2).
builder.Services.AddScoped<ICompanyRepository, CompanyRepository>();
builder.Services.AddScoped<IItemRepository, ItemRepository>();
builder.Services.AddScoped<IUomRepository, UomRepository>();
builder.Services.AddScoped<IWarehouseRepository, WarehouseRepository>();
builder.Services.AddScoped<IStockRepository, StockRepository>();
builder.Services.AddScoped<IIdempotencyRepository, IdempotencyRepository>();
builder.Services.AddScoped<IStockPostingService, StockPostingService>();

builder.Services.AddScoped<ICommandHandler<CreateItemCommand, Result<ItemDto>>, CreateItemCommandHandler>();
builder.Services.AddScoped<IQueryHandler<GetItemsQuery, IReadOnlyList<ItemDto>>, GetItemsQueryHandler>();
builder.Services.AddScoped<ICommandHandler<CreateWarehouseCommand, Result<WarehouseDto>>, CreateWarehouseCommandHandler>();
builder.Services.AddScoped<IQueryHandler<GetWarehousesQuery, IReadOnlyList<WarehouseTreeNodeDto>>, GetWarehousesQueryHandler>();
builder.Services.AddScoped<ICommandHandler<CreateStockEntryCommand, Result<StockEntryPostingDto>>, CreateStockEntryCommandHandler>();
builder.Services.AddScoped<ICommandHandler<CancelStockEntryCommand, Result<bool>>, CancelStockEntryCommandHandler>();
builder.Services.AddScoped<IQueryHandler<GetStockEntriesQuery, IReadOnlyList<StockEntryDto>>, GetStockEntriesQueryHandler>();

// Buying Cycle (Tasks 4.1-4.3): supplier master, purchase order workflow and the buying posting
// engine (accrual on receipt, interim clearance + A/P + Input Tax on invoice). Same split as the
// stock module: repositories in Erp.Infrastructure, handlers and services in Erp.Application.
builder.Services.AddScoped<ISupplierRepository, SupplierRepository>();
builder.Services.AddScoped<IPurchaseRepository, PurchaseRepository>();
builder.Services.AddScoped<IPurchasePostingService, PurchasePostingService>();

builder.Services.AddScoped<ICommandHandler<CreateSupplierCommand, Result<SupplierDto>>, CreateSupplierCommandHandler>();
builder.Services.AddScoped<IQueryHandler<GetSuppliersQuery, IReadOnlyList<SupplierDto>>, GetSuppliersQueryHandler>();
builder.Services.AddScoped<ICommandHandler<CreatePurchaseOrderCommand, Result<PurchaseOrderDto>>, CreatePurchaseOrderCommandHandler>();
builder.Services.AddScoped<ICommandHandler<UpdatePurchaseOrderCommand, Result<PurchaseOrderDto>>, UpdatePurchaseOrderCommandHandler>();
builder.Services.AddScoped<ICommandHandler<SubmitPurchaseOrderCommand, Result<PurchaseOrderDto>>, SubmitPurchaseOrderCommandHandler>();
builder.Services.AddScoped<IQueryHandler<GetPurchaseOrdersQuery, IReadOnlyList<PurchaseOrderDto>>, GetPurchaseOrdersQueryHandler>();
builder.Services.AddScoped<ICommandHandler<PostPurchaseReceiptCommand, Result<PurchaseReceiptPostingDto>>, PostPurchaseReceiptCommandHandler>();
builder.Services.AddScoped<IQueryHandler<GetPurchaseReceiptsQuery, IReadOnlyList<PurchaseReceiptDto>>, GetPurchaseReceiptsQueryHandler>();
builder.Services.AddScoped<ICommandHandler<PostPurchaseInvoiceCommand, Result<PurchaseInvoicePostingDto>>, PostPurchaseInvoiceCommandHandler>();
builder.Services.AddScoped<ICommandHandler<CancelPurchaseInvoiceCommand, Result<PurchaseInvoiceDto>>, CancelPurchaseInvoiceCommandHandler>();
builder.Services.AddScoped<IQueryHandler<GetPurchaseInvoicesQuery, IReadOnlyList<PurchaseInvoiceDto>>, GetPurchaseInvoicesQueryHandler>();

// Selling (Task 5.1): customer master, the plan.md §2 credit-limit gate's owner. Same split as
// the other modules: repository in Erp.Infrastructure, handlers in Erp.Application - only the
// composition root knows both (decision C2).
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<ICommandHandler<CreateCustomerCommand, Result<CustomerDto>>, CreateCustomerCommandHandler>();
builder.Services.AddScoped<IQueryHandler<GetCustomersQuery, IReadOnlyList<CustomerDto>>, GetCustomersQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetCustomerByIdQuery, CustomerDto?>, GetCustomerByIdQueryHandler>();

// Selling cycle (Tasks 5.2/5.2b): the sales order workflow, the delivery-note posting engine and
// the reads behind both documents. One EF class (SalesRepository) implements the two contracts -
// both write through the SAME scoped AppDbContext, so the posting transaction opened by either
// interface is ambient for the other.
builder.Services.AddScoped<ISalesOrderRepository, SalesRepository>();
builder.Services.AddScoped<IDeliveryNoteRepository, SalesRepository>();
builder.Services.AddScoped<ISalesInvoiceRepository, SalesInvoiceRepository>();
builder.Services.AddScoped<IPOSProfileRepository, POSProfileRepository>();
builder.Services.AddScoped<ISalesPostingService, SalesPostingService>();

builder.Services.AddScoped<ICommandHandler<SubmitPOSInvoiceCommand, Result<SalesInvoiceDto>>, SubmitPOSInvoiceCommandHandler>();
builder.Services.AddScoped<ICommandHandler<CreateSalesInvoiceCommand, Result<SalesInvoiceDto>>, CreateSalesInvoiceCommandHandler>();
builder.Services.AddScoped<ICommandHandler<SubmitSalesInvoiceCommand, Result<SalesInvoiceDto>>, SubmitSalesInvoiceCommandHandler>();

builder.Services.AddScoped<ICommandHandler<CreateSalesOrderCommand, Result<SalesOrderDto>>, CreateSalesOrderCommandHandler>();
builder.Services.AddScoped<ICommandHandler<SubmitSalesOrderCommand, Result<SalesOrderDto>>, SubmitSalesOrderCommandHandler>();
builder.Services.AddScoped<IQueryHandler<GetSalesOrdersQuery, IReadOnlyList<SalesOrderDto>>, GetSalesOrdersQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetSalesOrderByIdQuery, SalesOrderDto?>, GetSalesOrderByIdQueryHandler>();
builder.Services.AddScoped<ICommandHandler<PostDeliveryNoteCommand, Result<DeliveryNotePostingDto>>, PostDeliveryNoteCommandHandler>();
builder.Services.AddScoped<IQueryHandler<GetDeliveryNotesQuery, IReadOnlyList<DeliveryNoteDto>>, GetDeliveryNotesQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetDeliveryNoteByIdQuery, DeliveryNoteDto?>, GetDeliveryNoteByIdQueryHandler>();

// Journal Entry pipeline (tasks.md 2.3/2.4): the manual-voucher aggregate, the gapless JV number
// and the ATOMIC submit/cancel ledger appends. Same split as every other module: repository in
// Erp.Infrastructure, commands/queries/DTOs in Erp.Application - only the composition root knows
// both (decision C2).
builder.Services.AddScoped<IJournalRepository, JournalRepository>();
builder.Services.AddScoped<ICommandHandler<CreateJournalEntryCommand, Result<JournalEntryDto>>, CreateJournalEntryCommandHandler>();
builder.Services.AddScoped<ICommandHandler<SubmitJournalEntryCommand, Result<JournalEntryDto>>, SubmitJournalEntryCommandHandler>();
builder.Services.AddScoped<ICommandHandler<CancelJournalEntryCommand, Result<JournalEntryDto>>, CancelJournalEntryCommandHandler>();
builder.Services.AddScoped<IQueryHandler<GetJournalEntriesQuery, IReadOnlyList<JournalEntryDto>>, GetJournalEntriesQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetJournalEntryQuery, JournalEntryDto?>, GetJournalEntryQueryHandler>();

// Financial Reporting (tasks.md 2.5): the four read-only report queries over GLEntry. The ledger
// repository exposes NO write path (Constitution III.2 - append-only ledger), so a report can
// never mutate what it measures. Same split as every other module: repository in Erp.Infrastructure,
// queries/handlers/DTOs in Erp.Application - only the composition root knows both (decision C2).
builder.Services.AddScoped<IGLEntryRepository, GLEntryRepository>();
builder.Services.AddScoped<IQueryHandler<GetGeneralLedgerQuery, GeneralLedgerReportDto>, GetGeneralLedgerQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetTrialBalanceQuery, TrialBalanceReportDto>, GetTrialBalanceQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetBalanceSheetQuery, BalanceSheetReportDto>, GetBalanceSheetQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetProfitAndLossQuery, ProfitAndLossReportDto>, GetProfitAndLossQueryHandler>();

// Banking & Reconciliation staging (tasks.md 6.1/6.2): the statement import engine and its
// parsers. Same split as every other module: repository in Erp.Infrastructure, commands/parsers
// in Erp.Application - only the composition root knows both (decision C2). NO GL dependency
// anywhere on the import path (invariant BN-01 by construction).
builder.Services.AddScoped<IBankRepository, BankRepository>();
builder.Services.AddScoped<ICsvStatementParser, CsvStatementParser>();
builder.Services.AddScoped<IOfxStatementParser, OfxStatementParser>();
builder.Services.AddScoped<ICommandHandler<ImportBankStatementCommand, Result<BankStatementImportSummary>>, ImportBankStatementCommandHandler>();

// Banking rules engine + reconciliation (Block B, tasks 6.3/6.4): the heuristic rule run, the
// dual-sided reconcile / un-reconcile transitions, rule management and the staging reads
// behind the workbench controllers. The reconcile handler reads GL vouchers through the
// existing read-only IGLEntryRepository (zero GL writes - GLEntry is append-only).
builder.Services.AddScoped<ICommandHandler<ApplyMatchingRulesCommand, Result<RuleMatchSummary>>, ApplyMatchingRulesCommandHandler>();
builder.Services.AddScoped<ICommandHandler<CreateBankTransactionRuleCommand, Result<BankTransactionRuleDto>>, CreateBankTransactionRuleCommandHandler>();
builder.Services.AddScoped<ICommandHandler<ReconcileBankTransactionCommand, Result<ReconciliationSummary>>, ReconcileBankTransactionCommandHandler>();
builder.Services.AddScoped<ICommandHandler<UnreconcileBankTransactionCommand, Result<bool>>, UnreconcileBankTransactionCommandHandler>();
builder.Services.AddScoped<ICommandHandler<CreateVoucherFromBankTransactionCommand, Result<JournalEntryDto>>, CreateVoucherFromBankTransactionCommandHandler>();
builder.Services.AddScoped<IQueryHandler<GetBankTransactionsQuery, IReadOnlyList<BankTransactionDto>>, GetBankTransactionsQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetBankTransactionRulesQuery, IReadOnlyList<BankTransactionRuleDto>>, GetBankTransactionRulesQueryHandler>();

// Manufacturing (Block A masters + Block B work-order workflow and postings, tasks 9.1-9.4,
// plus the Block C cancel/reversal and UI reads, tasks 9.5-9.7):
// the master/workflow repository, the manufacture posting engine and the commands/queries
// behind the work-orders and BOMs controllers. Same split as every other module: repository in
// Erp.Infrastructure, handlers and services in Erp.Application - only the composition root
// knows both (decision C2).
builder.Services.AddScoped<IManufacturingRepository, ManufacturingRepository>();
builder.Services.AddScoped<IManufacturingPostingService, ManufacturingPostingService>();
builder.Services.AddScoped<ICommandHandler<CreateWorkOrderCommand, Result<WorkOrderDto>>, CreateWorkOrderCommandHandler>();
builder.Services.AddScoped<ICommandHandler<SubmitWorkOrderCommand, Result<WorkOrderDto>>, SubmitWorkOrderCommandHandler>();
builder.Services.AddScoped<ICommandHandler<TransferMaterialsToWipCommand, Result<StockEntryPostingDto>>, TransferMaterialsToWipCommandHandler>();
builder.Services.AddScoped<ICommandHandler<CompleteManufactureCommand, Result<StockEntryPostingDto>>, CompleteManufactureCommandHandler>();
builder.Services.AddScoped<ICommandHandler<CancelWorkOrderCommand, Result<WorkOrderDto>>, CancelWorkOrderCommandHandler>();
builder.Services.AddScoped<IQueryHandler<GetBomsQuery, IReadOnlyList<BomDto>>, GetBomsQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetBomDetailQuery, BomDto?>, GetBomDetailQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetWorkOrdersQuery, IReadOnlyList<WorkOrderDto>>, GetWorkOrdersQueryHandler>();

// Fixed Assets (Block A masters + capitalization, tasks 10.1-10.3): the category/asset/schedule
// repository and the commands behind the asset controllers. Same split as every other module:
// repository in Erp.Infrastructure, handlers and DTOs in Erp.Application - only the composition
// root knows both (decision C2). Registered from day one (banking precedent) so Block B
// (depreciation runs, disposal) builds on the same composition.
builder.Services.AddScoped<IAssetsRepository, AssetsRepository>();
builder.Services.AddScoped<ICommandHandler<CreateAssetCategoryCommand, Result<AssetCategoryDto>>, CreateAssetCategoryCommandHandler>();
builder.Services.AddScoped<ICommandHandler<CapitalizeAssetCommand, Result<AssetCapitalizationDto>>, CapitalizeAssetCommandHandler>();
builder.Services.AddScoped<ICommandHandler<PostDueDepreciationsCommand, Result<DepreciationRunDto>>, PostDueDepreciationsCommandHandler>();
builder.Services.AddScoped<ICommandHandler<DisposeAssetCommand, Result<AssetDisposalDto>>, DisposeAssetCommandHandler>();
builder.Services.AddScoped<ICommandHandler<CancelDisposeAssetCommand, Result<AssetDisposalReversalDto>>, CancelDisposeAssetCommandHandler>();
builder.Services.AddScoped<IQueryHandler<GetAssetCategoriesQuery, IReadOnlyList<AssetCategoryDto>>, GetAssetCategoriesQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetAssetsQuery, IReadOnlyList<AssetDto>>, GetAssetsQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetAssetDetailQuery, AssetDetailDto?>, GetAssetDetailQueryHandler>();

// Human Resources & Payroll masters (Tasks 12.1-12.2): salary components, structures and
// assignments. Same split as every other module: repository in Erp.Infrastructure, handlers
// and DTOs in Erp.Application - only the composition root knows both (decision C2).
// Registered from day one (banking precedent) so Block B (batch engine, Tasks 12.3-12.4)
// builds on the same composition.
builder.Services.AddScoped<IHrPayrollRepository, HrPayrollRepository>();
builder.Services.AddScoped<ICommandHandler<CreateSalaryComponentCommand, Result<SalaryComponentDto>>, CreateSalaryComponentCommandHandler>();
builder.Services.AddScoped<ICommandHandler<CreateSalaryStructureCommand, Result<SalaryStructureDto>>, CreateSalaryStructureCommandHandler>();
builder.Services.AddScoped<ICommandHandler<AssignSalaryStructureCommand, Result<SalaryStructureAssignmentDto>>, AssignSalaryStructureCommandHandler>();
builder.Services.AddScoped<ICommandHandler<SubmitPayrollRunCommand, Result<PayrollSubmitResultDto>>, SubmitPayrollRunCommandHandler>();
builder.Services.AddScoped<ICommandHandler<DisbursePayrollCommand, Result<PayrollEntryDto>>, DisbursePayrollCommandHandler>();
builder.Services.AddScoped<ICommandHandler<CancelPayrollCommand, Result<PayrollEntryDto>>, CancelPayrollCommandHandler>();
builder.Services.AddScoped<IQueryHandler<GetPayrollEntriesQuery, IReadOnlyList<PayrollEntryDto>>, GetPayrollEntriesQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetPayrollEntryQuery, PayrollEntryDetailDto?>, GetPayrollEntryQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetEmployeesQuery, IReadOnlyList<EmployeeDto>>, GetEmployeesQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetSalaryComponentsQuery, IReadOnlyList<SalaryComponentDto>>, GetSalaryComponentsQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetSalaryStructuresQuery, IReadOnlyList<SalaryStructureDto>>, GetSalaryStructuresQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetStructureAssignmentsQuery, IReadOnlyList<SalaryStructureAssignmentDto>>, GetStructureAssignmentsQueryHandler>();

// CRM & Sales Pipeline (Block A, tasks 11.1-11.7): lead/opportunity repository (EF
// CrmRepository implements both the adopted ICrmRepository and the new
// ICrmActivityRepository through the SAME scoped AppDbContext - the SalesRepository dual
// precedent) and the three adopted command handlers. Registered from day one (banking
// precedent); stage-advance/audit controllers land in Block B.
builder.Services.AddScoped<ICrmRepository, CrmRepository>();
builder.Services.AddScoped<ICrmActivityRepository, CrmRepository>();
builder.Services.AddScoped<ICommandHandler<IngestLeadCommand, Result<IngestLeadResultDto>>, IngestLeadCommandHandler>();
builder.Services.AddScoped<ICommandHandler<ConvertLeadCommand, Result<ConvertLeadResultDto>>, ConvertLeadCommandHandler>();
builder.Services.AddScoped<ICommandHandler<ReopenOpportunityCommand, Result<Guid>>, ReopenOpportunityCommandHandler>();
builder.Services.AddScoped<ICommandHandler<AdvanceOpportunityStageCommand, Result<OpportunityDto>>, AdvanceOpportunityStageCommandHandler>();
builder.Services.AddScoped<IQueryHandler<GetLeadsQuery, IReadOnlyList<LeadDto>>, GetLeadsQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetOpportunitiesQuery, IReadOnlyList<OpportunityDto>>, GetOpportunitiesQueryHandler>();

// [IdempotencyKeyRequired] is a ServiceFilterAttribute, so the filter itself must be resolvable
// from DI (Constitution Article VI.4).
builder.Services.AddScoped<IdempotencyFilter>();

// Constitution Article VI.1: the TenantMember policy. Phase 2 has NO authentication task, so the
// requirement is "TenantResolutionMiddleware resolved a tenant" (TenantMemberHandler). Add
// RequireAuthenticatedUser() + membership checks when bearer auth lands; a requirement-only policy
// needs no AddAuthentication registration and adds no auth packages (verified empirically).
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("TenantMember", policy => policy.AddRequirements(new TenantMemberRequirement()));
});
builder.Services.AddScoped<IAuthorizationHandler, TenantMemberHandler>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Tenant pipeline first: resolution must run before the logging scope opens, because the scope
// reads the already-resolved tenant (plan.md §2.1 request lifecycle).
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseMiddleware<TenantLoggingScopeMiddleware>();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

// Aspire health check endpoints (/health and /alive) - Development only.
app.MapDefaultEndpoints();

app.Run();

/// <summary>
/// Exposes the top-level-statement entry point as a public type so Task 1.3's HTTP integration
/// tests (tests/Erp.Api.IntegrationTests) can reference it as WebApplicationFactory&lt;Program&gt;.
/// Minimal change: top-level programs compile to an internal class without this declaration.
/// </summary>
// public partial class Program
// {
// }
