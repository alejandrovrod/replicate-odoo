using System.Text.Json.Serialization;
using Erp.Api.Authorization;
using Erp.Api.Filters;
using Erp.Api.Middleware;
using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.SystemBase.Companies;
using Erp.Application.Features.Accounts.Commands;
using Erp.Application.Features.Accounts.Queries;
using Erp.Application.Features.Banking.Commands;
using Erp.Application.Features.Banking.Parsers;
using Erp.Application.Features.Banking.Queries;
using Erp.Application.Features.Buying.Commands;
using Erp.Application.Features.Buying.Queries;
using Erp.Application.Features.GeneralLedger.Commands;
using Erp.Application.Features.GeneralLedger.PeriodClosing;
using Erp.Application.Features.GeneralLedger.Queries;
using Erp.Application.Features.FiscalClosing;
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
using Erp.Application.Features.Catalogs;
using Erp.Application.Features.Currencies.Commands;
using Erp.Application.Features.Payments.Commands;
using Erp.Application.Features.Payments.Queries;
using Erp.Application.Features.ExchangeRates.Commands;
using Erp.Application.Features.ExchangeRates.Queries;
using Erp.Application.Services;
using Erp.Domain.Common;
using Erp.Domain.Repositories;
using Erp.Infrastructure.Data;
using Erp.Infrastructure.Data.Repositories;
using Erp.Infrastructure.Seeders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
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

// i18n (spec 00-i18n): en/es with English as default AND fallback. Resources live under
// Resources/{Shared,Common,Controllers}/... (manifest {RootNamespace}.Resources.{path}); the
// neutral (culture-less) .resx holds the English text because the resource fallback chain for
// "es" is es -> neutral and never es -> "en" satellites. The provider order is the ASP.NET
// default which already matches the spec: QueryString (?culture=es, tests) -> Cookie (future
// user preference) -> Accept-Language -> default "en".
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.SetDefaultCulture("en")
        .AddSupportedCultures("en", "es")
        .AddSupportedUICultures("en", "es");
});

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
builder.Services.AddScoped<ICommandHandler<UpdateAccountCommand, Result<AccountDto>>, UpdateAccountCommandHandler>();
builder.Services.AddScoped<IQueryHandler<GetAccountTreeQuery, IReadOnlyList<AccountTreeNodeDto>>, GetAccountTreeQueryHandler>();

// Global currency catalog (RM-09): shared ISO master, no tenant scope.
builder.Services.AddScoped<ICurrencyRepository, CurrencyRepository>();
builder.Services.AddScoped<ICommandHandler<CreateCurrencyCommand, Result<CurrencyDto>>, CreateCurrencyCommandHandler>();
builder.Services.AddScoped<ICommandHandler<UpdateCurrencyCommand, Result<CurrencyDto>>, UpdateCurrencyCommandHandler>();

// Exchange Rates & Revaluations
builder.Services.AddScoped<IExchangeRateRepository, ExchangeRateRepository>();
builder.Services.AddScoped<IExchangeRateRevaluationRepository, ExchangeRateRevaluationRepository>();

builder.Services.AddScoped<ICommandHandler<UpsertExchangeRateCommand, Result<UpsertExchangeRateResult>>, UpsertExchangeRateCommandHandler>();
builder.Services.AddScoped<ICommandHandler<CreateExchangeRateRevaluationCommand, Result<CreateExchangeRateRevaluationResult>>, CreateExchangeRateRevaluationCommandHandler>();
builder.Services.AddScoped<ICommandHandler<SubmitExchangeRateRevaluationCommand, Result<SubmitExchangeRateRevaluationResult>>, SubmitExchangeRateRevaluationCommandHandler>();
builder.Services.AddScoped<ICommandHandler<CancelExchangeRateRevaluationCommand, Result<CancelExchangeRateRevaluationResult>>, CancelExchangeRateRevaluationCommandHandler>();

builder.Services.AddScoped<IQueryHandler<GetExchangeRatesQuery, IReadOnlyList<ExchangeRateDto>>, GetExchangeRatesQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetExchangeRateQuery, Result<GetExchangeRateResult>>, GetExchangeRateQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetExchangeRateRevaluationsQuery, Result<List<ExchangeRateRevaluationDto>>>, GetExchangeRateRevaluationsQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetExchangeRateRevaluationDetailQuery, Result<ExchangeRateRevaluationDetailDto>>, GetExchangeRateRevaluationDetailQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetRevaluationPreviewQuery, Result<RevaluationPreviewDto>>, GetRevaluationPreviewQueryHandler>();
// Stock & Inventory (Tasks 3.1-3.3): masters, the perpetual-inventory posting engine and the
// Article VI.4 idempotency filter. Repositories stay in Erp.Infrastructure, handlers in
// Erp.Application - only the composition root knows both (decision C2).
builder.Services.AddScoped<ICompanyRepository, CompanyRepository>();
builder.Services.AddScoped<IQueryHandler<GetCompanyQuery, Result<CompanyDto>>, GetCompanyQueryHandler>();
builder.Services.AddScoped<ICommandHandler<UpdateCompanyCommand, Result<CompanyDto>>, UpdateCompanyCommandHandler>();
builder.Services.AddScoped<IItemRepository, ItemRepository>();
builder.Services.AddScoped<IUomRepository, UomRepository>();
builder.Services.AddScoped<IWarehouseRepository, WarehouseRepository>();
builder.Services.AddScoped<IStockRepository, StockRepository>();
builder.Services.AddScoped<IIdempotencyRepository, IdempotencyRepository>();
builder.Services.AddScoped<IStockPostingService, StockPostingService>();

builder.Services.AddScoped<ICommandHandler<CreateItemCommand, Result<ItemDto>>, CreateItemCommandHandler>();
builder.Services.AddScoped<ICommandHandler<UpdateItemCommand, Result<ItemDto>>, UpdateItemCommandHandler>();
builder.Services.AddScoped<IQueryHandler<GetItemsQuery, PagedResult<ItemDto>>, GetItemsQueryHandler>();
builder.Services.AddScoped<ICommandHandler<CreateWarehouseCommand, Result<WarehouseDto>>, CreateWarehouseCommandHandler>();
builder.Services.AddScoped<ICommandHandler<UpdateWarehouseCommand, Result<WarehouseDto>>, UpdateWarehouseCommandHandler>();
builder.Services.AddScoped<IQueryHandler<GetWarehousesQuery, IReadOnlyList<WarehouseTreeNodeDto>>, GetWarehousesQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetFlatWarehousesQuery, PagedResult<WarehouseDto>>, GetFlatWarehousesQueryHandler>();
builder.Services.AddScoped<ICommandHandler<CreateStockEntryCommand, Result<StockEntryPostingDto>>, CreateStockEntryCommandHandler>();
builder.Services.AddScoped<ICommandHandler<CancelStockEntryCommand, Result<bool>>, CancelStockEntryCommandHandler>();
builder.Services.AddScoped<IQueryHandler<GetStockEntriesQuery, PagedResult<StockEntryDto>>, GetStockEntriesQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetStockSummaryQuery, StockSummaryDto>, GetStockSummaryQueryHandler>();

// Buying Cycle (Tasks 4.1-4.3): supplier master, purchase order workflow and the buying posting
// engine (accrual on receipt, interim clearance + A/P + Input Tax on invoice). Same split as the
// stock module: repositories in Erp.Infrastructure, handlers and services in Erp.Application.
builder.Services.AddScoped<ISupplierRepository, SupplierRepository>();
builder.Services.AddScoped<IPurchaseRepository, PurchaseRepository>();
builder.Services.AddScoped<IPurchasePostingService, PurchasePostingService>();

builder.Services.AddScoped<ICommandHandler<CreateSupplierCommand, Result<SupplierDto>>, CreateSupplierCommandHandler>();
builder.Services.AddScoped<ICommandHandler<UpdateSupplierCommand, Result<SupplierDto>>, UpdateSupplierCommandHandler>();
builder.Services.AddScoped<IQueryHandler<GetSuppliersQuery, PagedResult<SupplierDto>>, GetSuppliersQueryHandler>();
builder.Services.AddScoped<ICommandHandler<CreatePurchaseOrderCommand, Result<PurchaseOrderDto>>, CreatePurchaseOrderCommandHandler>();
builder.Services.AddScoped<ICommandHandler<UpdatePurchaseOrderCommand, Result<PurchaseOrderDto>>, UpdatePurchaseOrderCommandHandler>();
builder.Services.AddScoped<ICommandHandler<SubmitPurchaseOrderCommand, Result<PurchaseOrderDto>>, SubmitPurchaseOrderCommandHandler>();
builder.Services.AddScoped<IQueryHandler<GetPurchaseOrdersQuery, PagedResult<PurchaseOrderDto>>, GetPurchaseOrdersQueryHandler>();
builder.Services.AddScoped<ICommandHandler<PostPurchaseReceiptCommand, Result<PurchaseReceiptPostingDto>>, PostPurchaseReceiptCommandHandler>();
builder.Services.AddScoped<IQueryHandler<GetPurchaseReceiptsQuery, PagedResult<PurchaseReceiptDto>>, GetPurchaseReceiptsQueryHandler>();
builder.Services.AddScoped<ICommandHandler<PostPurchaseInvoiceCommand, Result<PurchaseInvoicePostingDto>>, PostPurchaseInvoiceCommandHandler>();
builder.Services.AddScoped<ICommandHandler<CancelPurchaseInvoiceCommand, Result<PurchaseInvoiceDto>>, CancelPurchaseInvoiceCommandHandler>();
builder.Services.AddScoped<IQueryHandler<GetPurchaseInvoicesQuery, PagedResult<PurchaseInvoiceDto>>, GetPurchaseInvoicesQueryHandler>();

// Selling (Task 5.1): customer master, the plan.md §2 credit-limit gate's owner. Same split as
// the other modules: repository in Erp.Infrastructure, handlers in Erp.Application - only the
// composition root knows both (decision C2).
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<ICommandHandler<CreateCustomerCommand, Result<CustomerDto>>, CreateCustomerCommandHandler>();
builder.Services.AddScoped<ICommandHandler<UpdateCustomerCommand, Result<CustomerDto>>, UpdateCustomerCommandHandler>();
builder.Services.AddScoped<IQueryHandler<GetCustomersQuery, PagedResult<CustomerDto>>, GetCustomersQueryHandler>();
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
builder.Services.AddScoped<IQueryHandler<GetSalesInvoicesQuery, PagedResult<SalesInvoiceDto>>, GetSalesInvoicesQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetSalesInvoiceByIdQuery, SalesInvoiceDto?>, GetSalesInvoiceByIdQueryHandler>();

builder.Services.AddScoped<ICommandHandler<CreateSalesOrderCommand, Result<SalesOrderDto>>, CreateSalesOrderCommandHandler>();
builder.Services.AddScoped<ICommandHandler<SubmitSalesOrderCommand, Result<SalesOrderDto>>, SubmitSalesOrderCommandHandler>();
builder.Services.AddScoped<IQueryHandler<GetSalesOrdersQuery, PagedResult<SalesOrderDto>>, GetSalesOrdersQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetSalesOrderByIdQuery, SalesOrderDto?>, GetSalesOrderByIdQueryHandler>();
builder.Services.AddScoped<ICommandHandler<PostDeliveryNoteCommand, Result<DeliveryNotePostingDto>>, PostDeliveryNoteCommandHandler>();
builder.Services.AddScoped<IQueryHandler<GetDeliveryNotesQuery, PagedResult<DeliveryNoteDto>>, GetDeliveryNotesQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetDeliveryNoteByIdQuery, DeliveryNoteDto?>, GetDeliveryNoteByIdQueryHandler>();

// Journal Entry pipeline (tasks.md 2.3/2.4): the manual-voucher aggregate, the gapless JV number
// and the ATOMIC submit/cancel ledger appends. Same split as every other module: repository in
// Erp.Infrastructure, commands/queries/DTOs in Erp.Application - only the composition root knows
// both (decision C2).
builder.Services.AddScoped<IJournalRepository, JournalRepository>();
builder.Services.AddScoped<ICommandHandler<CreateJournalEntryCommand, Result<JournalEntryDto>>, CreateJournalEntryCommandHandler>();
builder.Services.AddScoped<ICommandHandler<SubmitJournalEntryCommand, Result<JournalEntryDto>>, SubmitJournalEntryCommandHandler>();
builder.Services.AddScoped<ICommandHandler<CancelJournalEntryCommand, Result<JournalEntryDto>>, CancelJournalEntryCommandHandler>();
builder.Services.AddScoped<IQueryHandler<GetJournalEntriesQuery, PagedResult<JournalEntryDto>>, GetJournalEntriesQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetJournalEntryQuery, JournalEntryDto?>, GetJournalEntryQueryHandler>();

// Fiscal Closing (R-13, spec 13-fiscal-closing): the fiscal-year master, the hardened closing
// voucher pipeline (create Draft → submit atomic close → cancel by reversal) and the read-only
// P&L preview. Same split as every other module: repositories in Erp.Infrastructure,
// commands/queries/DTOs in Erp.Application - only the composition root knows both (decision C2).
builder.Services.AddScoped<IFiscalYearRepository, FiscalYearRepository>();
builder.Services.AddScoped<IPeriodClosingVoucherRepository, PeriodClosingVoucherRepository>();
builder.Services.AddScoped<ICommandHandler<CreateFiscalYearCommand, Result<FiscalYearDto>>, CreateFiscalYearCommandHandler>();
builder.Services.AddScoped<ICommandHandler<CloseFiscalYearCommand, Result<FiscalYearDto>>, CloseFiscalYearCommandHandler>();
builder.Services.AddScoped<IQueryHandler<GetFiscalYearsQuery, PagedResult<FiscalYearDto>>, GetFiscalYearsQueryHandler>();
builder.Services.AddScoped<ICommandHandler<CreatePeriodClosingVoucherCommand, Result<PeriodClosingVoucherDto>>, CreatePeriodClosingVoucherCommandHandler>();
builder.Services.AddScoped<ICommandHandler<SubmitPeriodClosingVoucherCommand, Result<PeriodClosingVoucherDto>>, SubmitPeriodClosingVoucherCommandHandler>();
builder.Services.AddScoped<ICommandHandler<CancelPeriodClosingVoucherCommand, Result<PeriodClosingVoucherDto>>, CancelPeriodClosingVoucherCommandHandler>();
builder.Services.AddScoped<IQueryHandler<GetPeriodClosingVouchersQuery, PagedResult<PeriodClosingVoucherDto>>, GetPeriodClosingVouchersQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetPeriodClosingVoucherDetailQuery, PeriodClosingVoucherDto?>, GetPeriodClosingVoucherDetailQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetUnclosedPLBalancesQuery, ClosingPreviewDto?>, GetUnclosedPLBalancesQueryHandler>();

// Financial Reporting (tasks.md 2.5): the four read-only report queries over GLEntry. The ledger
// repository exposes NO write path (Constitution III.2 - append-only ledger), so a report can
// never mutate what it measures. Same split as every other module: repository in Erp.Infrastructure,
// queries/handlers/DTOs in Erp.Application - only the composition root knows both (decision C2).
builder.Services.AddScoped<IGLEntryRepository, GLEntryRepository>();
builder.Services.AddScoped<IQueryHandler<GetGeneralLedgerQuery, GeneralLedgerReportDto>, GetGeneralLedgerQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetTrialBalanceQuery, TrialBalanceReportDto>, GetTrialBalanceQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetBalanceSheetQuery, BalanceSheetReportDto>, GetBalanceSheetQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetProfitAndLossQuery, ProfitAndLossReportDto>, GetProfitAndLossQueryHandler>();
builder.Services.AddScoped<IStockLedgerReportRepository, StockRepository>();
builder.Services.AddScoped<IReceivableAgingRepository, SalesInvoiceRepository>();
builder.Services.AddScoped<IPayableAgingRepository, PurchaseRepository>();
builder.Services.AddScoped<IQueryHandler<GetStockLedgerReportQuery, StockLedgerReportDto>, GetStockLedgerReportQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetAgingReportQuery, AgingReportDto>, GetAgingReportQueryHandler>();
// Banking & Reconciliation staging (tasks.md 6.1/6.2): the statement import engine and its
// parsers. Same split as every other module: repository in Erp.Infrastructure, commands/parsers
// in Erp.Application - only the composition root knows both (decision C2). NO GL dependency
// anywhere on the import path (invariant BN-01 by construction).
builder.Services.AddScoped<IBankRepository, BankRepository>();
builder.Services.AddScoped<IBankTransactionRuleEvaluator, BankTransactionRuleEvaluator>();
builder.Services.AddScoped<ICsvStatementParser, CsvStatementParser>();
builder.Services.AddScoped<IOfxStatementParser, OfxStatementParser>();
builder.Services.AddScoped<ICommandHandler<ImportBankStatementCommand, Result<BankStatementImportSummary>>, ImportBankStatementCommandHandler>();

// Banking rules engine + reconciliation (Block B, tasks 6.3/6.4): the heuristic rule run, the
// dual-sided reconcile / un-reconcile transitions, rule management and the staging reads
// behind the workbench controllers. The reconcile handler reads GL vouchers through the
// existing read-only IGLEntryRepository (zero GL writes - GLEntry is append-only).
builder.Services.AddScoped<ICommandHandler<ApplyMatchingRulesCommand, Result<RuleMatchSummary>>, ApplyMatchingRulesCommandHandler>();
builder.Services.AddScoped<ICommandHandler<CreateBankTransactionRuleCommand, Result<BankTransactionRuleDto>>, CreateBankTransactionRuleCommandHandler>();
builder.Services.AddScoped<ICommandHandler<CreateBankAccountCommand, Result<BankAccountDto>>, CreateBankAccountCommandHandler>();
builder.Services.AddScoped<ICommandHandler<UpdateBankAccountCommand, Result<BankAccountDto>>, UpdateBankAccountCommandHandler>();

// Payment entry & settlement (spec R-12): draft creation, idempotent submit posting with
// gapless PAY- numbering, compensating cancel, and the allocation-grid reads.
builder.Services.AddScoped<ICommandHandler<CreatePaymentEntryCommand, Result<PaymentEntryDto>>, CreatePaymentEntryCommandHandler>();
builder.Services.AddScoped<ICommandHandler<SubmitPaymentEntryCommand, Result<PaymentEntryDto>>, SubmitPaymentEntryCommandHandler>();
builder.Services.AddScoped<ICommandHandler<CancelPaymentEntryCommand, Result<PaymentEntryDto>>, CancelPaymentEntryCommandHandler>();
builder.Services.AddScoped<IQueryHandler<GetPaymentsQuery, PagedResult<PaymentEntryDto>>, GetPaymentsQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetPaymentDetailQuery, PaymentEntryDetailDto?>, GetPaymentDetailQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetOutstandingSalesInvoicesQuery, IReadOnlyList<OutstandingInvoiceDto>>, GetOutstandingSalesInvoicesQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetOutstandingPurchaseInvoicesQuery, IReadOnlyList<OutstandingInvoiceDto>>, GetOutstandingPurchaseInvoicesQueryHandler>();
builder.Services.AddScoped<ICommandHandler<ReconcileBankTransactionCommand, Result<ReconciliationSummary>>, ReconcileBankTransactionCommandHandler>();
builder.Services.AddScoped<ICommandHandler<UnreconcileBankTransactionCommand, Result<bool>>, UnreconcileBankTransactionCommandHandler>();
builder.Services.AddScoped<ICommandHandler<CreateVoucherFromBankTransactionCommand, Result<JournalEntryDto>>, CreateVoucherFromBankTransactionCommandHandler>();
builder.Services.AddScoped<IQueryHandler<GetBankTransactionsQuery, PagedResult<BankTransactionDto>>, GetBankTransactionsQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetBankTransactionRulesQuery, PagedResult<BankTransactionRuleDto>>, GetBankTransactionRulesQueryHandler>();

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
builder.Services.AddScoped<IQueryHandler<GetBomsQuery, PagedResult<BomDto>>, GetBomsQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetBomDetailQuery, BomDto?>, GetBomDetailQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetWorkOrdersQuery, PagedResult<WorkOrderDto>>, GetWorkOrdersQueryHandler>();

// Fixed Assets (Block A masters + capitalization, tasks 10.1-10.3): the category/asset/schedule
// repository and the commands behind the asset controllers. Same split as every other module:
// repository in Erp.Infrastructure, handlers and DTOs in Erp.Application - only the composition
// root knows both (decision C2). Registered from day one (banking precedent) so Block B
// (depreciation runs, disposal) builds on the same composition.
builder.Services.AddScoped<IAssetsRepository, AssetsRepository>();
builder.Services.AddScoped<ICommandHandler<CreateAssetCategoryCommand, Result<AssetCategoryDto>>, CreateAssetCategoryCommandHandler>();
builder.Services.AddScoped<ICommandHandler<UpdateAssetCategoryCommand, Result<AssetCategoryDto>>, UpdateAssetCategoryCommandHandler>();
builder.Services.AddScoped<ICommandHandler<CapitalizeAssetCommand, Result<AssetCapitalizationDto>>, CapitalizeAssetCommandHandler>();
builder.Services.AddScoped<ICommandHandler<PostDueDepreciationsCommand, Result<DepreciationRunDto>>, PostDueDepreciationsCommandHandler>();
builder.Services.AddScoped<ICommandHandler<DisposeAssetCommand, Result<AssetDisposalDto>>, DisposeAssetCommandHandler>();
builder.Services.AddScoped<ICommandHandler<CancelDisposeAssetCommand, Result<AssetDisposalReversalDto>>, CancelDisposeAssetCommandHandler>();
builder.Services.AddScoped<IQueryHandler<GetAssetCategoriesQuery, PagedResult<AssetCategoryDto>>, GetAssetCategoriesQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetAssetsQuery, PagedResult<AssetDto>>, GetAssetsQueryHandler>();
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
builder.Services.AddScoped<IQueryHandler<GetPayrollEntriesQuery, PagedResult<PayrollEntryDto>>, GetPayrollEntriesQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetPayrollEntryQuery, PayrollEntryDetailDto?>, GetPayrollEntryQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetEmployeesQuery, PagedResult<EmployeeDto>>, GetEmployeesQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetSalaryComponentsQuery, PagedResult<SalaryComponentDto>>, GetSalaryComponentsQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetSalaryStructuresQuery, PagedResult<SalaryStructureDto>>, GetSalaryStructuresQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetStructureAssignmentsQuery, PagedResult<SalaryStructureAssignmentDto>>, GetStructureAssignmentsQueryHandler>();

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
builder.Services.AddScoped<ICommandHandler<CreateOpportunitySalesOrderCommand, Result<SalesOrderDto>>, CreateOpportunitySalesOrderCommandHandler>();
builder.Services.AddScoped<IQueryHandler<GetLeadsQuery, PagedResult<LeadDto>>, GetLeadsQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetOpportunitiesQuery, PagedResult<OpportunityDto>>, GetOpportunitiesQueryHandler>();

// System Catalogs
builder.Services.AddScoped<ICatalogRepository, CatalogRepository>();
builder.Services.AddScoped<IQueryHandler<GetCatalogByCodeQuery, List<CatalogItemDto>>, GetCatalogByCodeQueryHandler>();

// [IdempotencyKeyRequired] is a ServiceFilterAttribute, so the filter itself must be resolvable
// from DI (Constitution Article VI.4).
builder.Services.AddScoped<IdempotencyFilter>();

// JWT Bearer Auth for R3 (RBAC & User Identity)
var jwtSecret = builder.Configuration["JwtSettings:Secret"] ?? "AspireErpSuperSecretKeyThatIsAtLeast32BytesLongForHS256!!!";
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = "AspireErp",
        ValidAudience = "AspireErp",
        IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtSecret))
    };
});

// Constitution Article VI.1: the TenantMember policy & Phase R3 dynamic RBAC.
// Phase 2 had NO authentication task, so the requirement was just "TenantResolutionMiddleware resolved a tenant" (TenantMemberHandler). 
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("TenantMember", policy => policy.AddRequirements(new TenantMemberRequirement()));
});
builder.Services.AddScoped<IAuthorizationHandler, TenantMemberHandler>();

// Dynamic Permission Policy Provider for [Authorize(Policy = "permission:doctype:read")]
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationPolicyProvider, DocTypePermissionPolicyProvider>();
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, DocTypePermissionAuthorizationHandler>();

// Security
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ITokenGenerator, Erp.Infrastructure.Security.JwtTokenGenerator>();
builder.Services.AddScoped<ICommandHandler<Erp.Application.Features.Security.Commands.LoginCommand, Result<Erp.Application.Features.Security.Commands.LoginResponseDto>>, Erp.Application.Features.Security.Commands.LoginCommandHandler>();

var app = builder.Build();

// Configure the HTTP request pipeline.

// i18n: resolve the request culture (QueryString -> Cookie -> Accept-Language -> "en") before
// any middleware or controller reads CultureInfo.CurrentUICulture (spec 00-i18n resolution order).
app.UseRequestLocalization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Ensure database is created and migrations are applied in Development
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.Migrate();

    // Selling Fase 5: every initialized company owns a Receivable leaf ("Deudores por
    // Ventas") so the Customer receivable picker is never empty on new companies.
    // Idempotent per company; runs alongside Migrate on every Development boot. Each company
    // gets its OWN scope with its tenant set: the seeder inserts tenant-stamped rows, which
    // the fail-closed tenant guard would otherwise reject on the shared tenantless scope.
    // Parallel boots (one WebApplicationFactory per test class) may race on the same company;
    // the seeder converges on the unique index instead of crashing the boot.
    var companies = await dbContext.Companies.IgnoreQueryFilters().ToListAsync();
    foreach (var bootstrappedCompany in companies)
    {
        using var companyScope = app.Services.CreateScope();
        companyScope.ServiceProvider.GetRequiredService<ITenantProvider>().SetCurrentTenantId(bootstrappedCompany.TenantId);
        var companyContext = companyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        await ReceivableAccountSeeder.SeedAsync(companyContext, bootstrappedCompany.Id, bootstrappedCompany.TenantId);
    }

    // Fase R3: seed the base RBAC matrix (roles + DocTypePermission rows) for every known
    // tenant, same per-tenant-scope discipline as above. Tenants enumerate tenantless - the
    // seeder only writes tenant-stamped rows inside the pinned scope.
    var tenantIds = await dbContext.Companies.IgnoreQueryFilters()
        .Select(c => c.TenantId)
        .Distinct()
        .ToListAsync();
        
    var devTenantId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    if (!tenantIds.Contains(devTenantId))
    {
        tenantIds.Add(devTenantId);
    }
    foreach (var tenantId in tenantIds)
    {
        using var tenantScope = app.Services.CreateScope();
        tenantScope.ServiceProvider.GetRequiredService<ITenantProvider>().SetCurrentTenantId(tenantId);
        var tenantContext = tenantScope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SecurityRoleSeeder.SeedAsync(tenantContext, tenantId);

        // Seed demo user for testing
        if (!await tenantContext.Users.AnyAsync(u => u.Email == "demo@example.com"))
        {
            var adminUser = new Erp.Domain.Entities.Security.User 
            { 
                Id = Guid.NewGuid(), 
                TenantId = tenantId,
                FullName = "System Admin", 
                Email = "demo@example.com", 
                PasswordHash = "demo"
            };
            tenantContext.Users.Add(adminUser);
            
            // Assign System Manager role
            var sysManagerRole = await tenantContext.Roles.FirstOrDefaultAsync(r => r.Name == "System Manager");
            if (sysManagerRole != null)
            {
                tenantContext.UserRoles.Add(new Erp.Domain.Entities.Security.UserRole 
                { 
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    UserId = adminUser.Id, 
                    RoleId = sysManagerRole.Id 
                });
            }
            await tenantContext.SaveChangesAsync();
        }
    }
}

// Tenant pipeline first: resolution must run before the logging scope opens, because the scope
// reads the already-resolved tenant (plan.md §2.1 request lifecycle).
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseMiddleware<TenantLoggingScopeMiddleware>();

app.UseHttpsRedirection();

app.UseAuthentication();
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
