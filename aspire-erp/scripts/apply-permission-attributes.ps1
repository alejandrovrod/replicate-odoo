# Fase R3: injects [Authorize(Policy = "permission:doctype:action")] per action.
# Idempotent: skips actions that already carry a permission: policy; skips AuthController.
# Mapping: GET -> read; route contains cancel|unreconcile|reopen|reverse-disposal -> cancel;
# route contains submit|approve|close|disburse|complete|capitalize|depreciation|transfer|convert|advance|run-rules|reconcile|create-sales-order -> submit;
# every other mutating action -> write.

$controllersDir = Join-Path $PSScriptRoot "..\src\Backend\Erp.Api\Controllers\V1"

$doctypes = @{
    "AccountsController.cs" = "account"
    "AssetCategoriesController.cs" = "asset_category"
    "AssetsController.cs" = "asset"
    "BankAccountsController.cs" = "bank_account"
    "BankStatementImportsController.cs" = "bank_statement_import"
    "BankTransactionRulesController.cs" = "bank_transaction_rule"
    "BankTransactionsController.cs" = "bank_transaction"
    "BomsController.cs" = "bom"
    "CatalogsController.cs" = "catalog"
    "CompaniesController.cs" = "company"
    "CurrenciesController.cs" = "currency"
    "CustomersController.cs" = "customer"
    "DeliveryNotesController.cs" = "delivery_note"
    "ExchangeRateRevaluationsController.cs" = "exchange_rate_revaluation"
    "ExchangeRatesController.cs" = "exchange_rate"
    "FinancialReportsController.cs" = "report"
    "FiscalYearsController.cs" = "fiscal_year"
    "HrPayrollMastersController.cs" = "__peraction__"
    "ItemsController.cs" = "item"
    "JournalEntriesController.cs" = "journal_entry"
    "LeadsController.cs" = "lead"
    "OpportunitiesController.cs" = "opportunity"
    "PaymentEntriesController.cs" = "payment_entry"
    "PayrollEntriesController.cs" = "payroll_entry"
    "PeriodClosingVouchersController.cs" = "period_closing_voucher"
    "ProjectsController.cs" = "project"
    "PurchaseInvoicesController.cs" = "purchase_invoice"
    "PurchaseOrdersController.cs" = "purchase_order"
    "PurchaseReceiptsController.cs" = "purchase_receipt"
    "SalesInvoicesController.cs" = "sales_invoice"
    "SalesOrdersController.cs" = "sales_order"
    "StockController.cs" = "stock"
    "StockEntriesController.cs" = "stock_entry"
    "SuppliersController.cs" = "supplier"
    "WarehousesController.cs" = "warehouse"
    "WorkOrdersController.cs" = "work_order"
}

# (controller, route-substring) -> doctype overrides for multi-doctype controllers
$perActionDoctype = @(
    @("HrController.cs", "attendance", "attendance"),
    @("HrController.cs", "leave-applications", "leave_application"),
    @("HrPayrollMastersController.cs", "employees", "employee"),
    @("HrPayrollMastersController.cs", "salary-components", "salary_component"),
    @("HrPayrollMastersController.cs", "salary-structures", "salary_structure"),
    @("HrPayrollMastersController.cs", "structure-assignments", "salary_structure")
)
$doctypes["HrController.cs"] = "__peraction__"

function Get-Perm($verb, $route) {
    if ($verb -eq "HttpGet") { return "read" }
    if ($route -match "cancel|unreconcile|reopen|reverse-disposal") { return "cancel" }
    if ($route -match "submit|approve|close|disburse|complete|capitalize|depreciation|transfer|convert|advance|run-rules|reconcile|create-sales-order") { return "submit" }
    return "write"
}

function Get-Doctype($file, $route, $default) {
    foreach ($rule in $perActionDoctype) {
        if ($rule[0] -eq $file -and $route -like ("*" + $rule[1] + "*")) { return $rule[2] }
    }
    return $default
}

$total = 0
foreach ($file in (Get-ChildItem $controllersDir -Filter "*.cs" | Sort-Object Name)) {
    if ($file.Name -eq "AuthController.cs") { continue }
    if (-not $doctypes.ContainsKey($file.Name)) { Write-Output ("NO-DOCTYPE: " + $file.Name); continue }

    $lines = [System.Collections.ArrayList]@(Get-Content $file.FullName)
    $changed = $false

    if (-not ($lines -match "^using Microsoft\.AspNetCore\.Authorization;$")) {
        $lastUsing = -1
        for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($lines[$i] -match "^using ") { $lastUsing = $i }
        }
        $lines.Insert($lastUsing + 1, "using Microsoft.AspNetCore.Authorization;") | Out-Null
        $changed = $true
    }

    # re-scan with updated indexes: find method lines, walk attribute block upward
    $i = 0
    while ($i -lt $lines.Count) {
        if ($lines[$i] -match "public async Task<IActionResult>") {
            $j = $i - 1
            $httpLine = -1
            $httpVerb = ""
            $httpRoute = ""
            $hasPerm = $false
            while ($j -ge 0 -and $lines[$j] -match "^\s*\[.*\]\s*$") {
                if ($lines[$j] -match 'Authorize\(Policy = "permission:') { $hasPerm = $true }
                if ($lines[$j] -match '^\s*\[(HttpGet|HttpPost|HttpPut|HttpPatch|HttpDelete)(?<route>\([^\)]*\))?') {
                    $httpLine = $j
                    $httpVerb = $Matches[1]
                    $httpRoute = $Matches["route"]
                }
                $j--
            }
            if ($httpLine -ge 0 -and -not $hasPerm) {
                $dt = $doctypes[$file.Name]
                if ($dt -eq "__peraction__") { $dt = Get-Doctype $file.Name $httpRoute "" }
                if ($dt -ne "") {
                    $perm = Get-Perm $httpVerb $httpRoute
                    $indent = [regex]::Match($lines[$httpLine], "^\s*").Value
                    $lines.Insert($httpLine + 1, ($indent + '[Authorize(Policy = "permission:' + $dt + ':' + $perm + '")]')) | Out-Null
                    $total++
                    $changed = $true
                    $i++ # account for inserted line
                }
            }
        }
        $i++
    }

    if ($changed) {
        Set-Content -Path $file.FullName -Value $lines
        Write-Output ("UPDATED: " + $file.Name)
    }
}
Write-Output ("TOTAL-INSERTED: " + $total)
