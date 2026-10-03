using System.Globalization;
using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.GeneralLedger.Queries;
using Erp.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers.V1;

/// <summary>
/// Financial Reporting endpoints (tasks.md 2.5): the four read-model queries over the immutable
/// General Ledger - general ledger, trial balance, balance sheet and profit &amp; loss. Attributes
/// follow Constitution Article VI: explicit route + versioning + TenantMember policy (VI.1),
/// JSON content negotiation (VI.2) and exhaustive status documentation (VI.3).
/// </summary>
/// <remarks>
/// <para><b>Tenancy.</b> Rows are scoped by TenantResolutionMiddleware (X-Tenant-ID) plus
/// AppDbContext's global query filter (Constitution II.3); every query names a COMPANY only. A
/// request for a company this tenant cannot see simply answers 200 with an empty report - the
/// same read semantics as the account tree, so no endpoint here ever 404s.</para>
/// <para><b>Dates arrive as strings</b> (pinned contract: <c>yyyy-MM-dd</c>) instead of
/// <c>DateOnly</c> so the endpoint OWNS the failure: an unparsable or missing period becomes an
/// RFC 7807 400 with a stable machine code, rather than the framework's automatic validation
/// problem, and a typo can never silently shift the statement to another period.</para>
/// <para><b>Status matrix</b> (read endpoints, so 400 is the only failure):
/// <c>company_required</c> (missing/empty companyId), <c>date_required</c> (a statement period the
/// caller omitted) and <c>invalid_date</c> (a date that does not parse) - all 400. No [Consumes]:
/// every action is a GET with no request body.</para>
/// </remarks>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "TenantMember")]
[Produces("application/json")]
public sealed class FinancialReportsController : ControllerBase
{
    private readonly ISender _sender;

    public FinancialReportsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// General ledger rows (chronological, PostingDate ASC then Id ASC) with the Debit/Credit
    /// totals of the FULL filter - the pinned Task 2.6 contract.
    /// </summary>
    /// <remarks>
    /// All filters are optional and combine with AND. <c>take</c> defaults to 500 and is capped at
    /// 5000 by the handler; the totals always describe every matching row, never just this page.
    /// </remarks>
    /// <param name="companyId">Company that owns the ledger.</param>
    /// <param name="accountId">Exact posting account to restrict the report to.</param>
    /// <param name="voucherId">Exact source-document id (voucher drill-down).</param>
    /// <param name="voucherType">Exact source-document type, e.g. "JournalEntry".</param>
    /// <param name="from">Inclusive first posting date (<c>yyyy-MM-dd</c>).</param>
    /// <param name="to">Inclusive last posting date (<c>yyyy-MM-dd</c>).</param>
    /// <param name="take">Maximum rows to return (default 500, max 5000).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet("general-ledger")]
    [ProducesResponseType(typeof(GeneralLedgerReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetGeneralLedger(
        [FromQuery] Guid companyId,
        [FromQuery] Guid? accountId = null,
        [FromQuery] Guid? voucherId = null,
        [FromQuery] string? voucherType = null,
        [FromQuery] string? from = null,
        [FromQuery] string? to = null,
        [FromQuery] int take = GeneralLedgerPaging.DefaultTake,
        CancellationToken cancellationToken = default)
    {
        if (companyId == Guid.Empty)
        {
            return Problem400(
                "Invalid Company",
                "The companyId query parameter must be a non-empty GUID.",
                FinancialReportErrorCodes.CompanyRequired);
        }

        if (!TryParseDate(from, out var fromDate))
        {
            return Problem400(
                "Invalid Date Range",
                "The from query parameter must be a valid calendar date in yyyy-MM-dd format.",
                FinancialReportErrorCodes.InvalidDate);
        }

        if (!TryParseDate(to, out var toDate))
        {
            return Problem400(
                "Invalid Date Range",
                "The to query parameter must be a valid calendar date in yyyy-MM-dd format.",
                FinancialReportErrorCodes.InvalidDate);
        }

        var report = await _sender.SendAsync(
            new GetGeneralLedgerQuery(companyId, accountId, voucherId, voucherType, fromDate, toDate, take),
            cancellationToken);

        return Ok(report);
    }

    /// <summary>
    /// Trial balance as of a date: per-account SUM(Debit)/SUM(Credit) with the column totals and
    /// their discrepancy (plan.md §4). Balanced data reports <c>difference == 0.0000</c> - the
    /// acceptance of tasks.md 2.5.
    /// </summary>
    /// <param name="companyId">Company that owns the ledger.</param>
    /// <param name="asOfDate">Inclusive cutoff date (<c>yyyy-MM-dd</c>). REQUIRED.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet("trial-balance")]
    [ProducesResponseType(typeof(TrialBalanceReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTrialBalance(
        [FromQuery] Guid companyId,
        [FromQuery] string? asOfDate = null,
        CancellationToken cancellationToken = default)
    {
        var invalidPeriod = ValidatePeriod(companyId, asOfDate, "asOfDate", out var cutoff);
        if (invalidPeriod is not null)
        {
            return invalidPeriod;
        }

        var report = await _sender.SendAsync(
            new GetTrialBalanceQuery(companyId, cutoff),
            cancellationToken);

        return Ok(report);
    }

    /// <summary>
    /// Balance sheet as of a date: assets / liabilities / equity sections in their natural signs
    /// plus the <c>balanced</c> flag (<c>|assets − (liabilities + equity)| ≤ 0.0001</c>). Income
    /// and Expense are excluded - they are the profit &amp; loss, so an unclosed period reports
    /// <c>balanced == false</c> by exactly its net profit.
    /// </summary>
    /// <param name="companyId">Company that owns the ledger.</param>
    /// <param name="asOfDate">Inclusive cutoff date (<c>yyyy-MM-dd</c>). REQUIRED.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet("balance-sheet")]
    [ProducesResponseType(typeof(BalanceSheetReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetBalanceSheet(
        [FromQuery] Guid companyId,
        [FromQuery] string? asOfDate = null,
        CancellationToken cancellationToken = default)
    {
        var invalidPeriod = ValidatePeriod(companyId, asOfDate, "asOfDate", out var cutoff);
        if (invalidPeriod is not null)
        {
            return invalidPeriod;
        }

        var report = await _sender.SendAsync(
            new GetBalanceSheetQuery(companyId, cutoff),
            cancellationToken);

        return Ok(report);
    }

    /// <summary>
    /// Profit &amp; loss for a period: revenue (Income root), COGS (Expense root typed COGS),
    /// operating expenses (every other Expense account) and
    /// <c>netProfit = revenue − cogs − expenses</c> - tasks.md 2.5's formula. A loss is a negative
    /// <c>netProfit</c>, never an error.
    /// </summary>
    /// <param name="companyId">Company that owns the ledger.</param>
    /// <param name="from">Inclusive first posting date (<c>yyyy-MM-dd</c>). REQUIRED.</param>
    /// <param name="to">Inclusive last posting date (<c>yyyy-MM-dd</c>). REQUIRED.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet("profit-and-loss")]
    [ProducesResponseType(typeof(ProfitAndLossReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetProfitAndLoss(
        [FromQuery] Guid companyId,
        [FromQuery] string? from = null,
        [FromQuery] string? to = null,
        CancellationToken cancellationToken = default)
    {
        if (companyId == Guid.Empty)
        {
            return Problem400(
                "Invalid Company",
                "The companyId query parameter must be a non-empty GUID.",
                FinancialReportErrorCodes.CompanyRequired);
        }

        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
        {
            return Problem400(
                "Missing Period",
                "The from and to query parameters are both required for a profit and loss statement.",
                FinancialReportErrorCodes.DateRequired);
        }

        // Parsed directly rather than through TryParseDate: both parameters are REQUIRED here (the
        // gate above proved they exist), so the result is a real period, never a null "no filter".
        if (!DateOnly.TryParse(
                from, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fromDate)
            || !DateOnly.TryParse(
                to, CultureInfo.InvariantCulture, DateTimeStyles.None, out var toDate))
        {
            return Problem400(
                "Invalid Period",
                "The from and to query parameters must be valid calendar dates in yyyy-MM-dd format.",
                FinancialReportErrorCodes.InvalidDate);
        }

        var report = await _sender.SendAsync(
            new GetProfitAndLossQuery(companyId, fromDate, toDate),
            cancellationToken);

        return Ok(report);
    }

    /// <summary>
    /// Shared gate of the two snapshot statements: company must be present, the single cutoff date
    /// must be sent AND parseable. Returns null when the request is well-formed and hands the
    /// parsed cutoff back through <paramref name="cutoff"/>.
    /// </summary>
    private ObjectResult? ValidatePeriod(
        Guid companyId,
        string? asOfDate,
        string parameterName,
        out DateOnly cutoff)
    {
        cutoff = default;

        if (companyId == Guid.Empty)
        {
            return Problem400(
                "Invalid Company",
                "The companyId query parameter must be a non-empty GUID.",
                FinancialReportErrorCodes.CompanyRequired);
        }

        if (string.IsNullOrWhiteSpace(asOfDate))
        {
            return Problem400(
                "Missing Date",
                $"The {parameterName} query parameter is required (yyyy-MM-dd).",
                FinancialReportErrorCodes.DateRequired);
        }

        // Parsed here rather than through TryParseDate so the out value stays a NON-nullable
        // DateOnly: this gate already proved the parameter is present, and the statement below
        // must receive a real cutoff rather than a null it would have to reject again.
        if (!DateOnly.TryParse(
                asOfDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out cutoff))
        {
            return Problem400(
                "Invalid Date",
                $"The {parameterName} query parameter must be a valid calendar date in yyyy-MM-dd format.",
                FinancialReportErrorCodes.InvalidDate);
        }

        return null;
    }

    /// <summary>
    /// Parses an OPTIONAL date query parameter. Null/whitespace means "unset" (no filter) and
    /// hands back <c>null</c>; any other value must parse as a calendar date, so a typo fails
    /// loudly instead of silently widening or shifting the report.
    /// </summary>
    /// <remarks>
    /// The out value is deliberately <see cref="Nullable{T}"/>: writing <c>default</c> into a
    /// non-nullable <c>DateOnly</c> would turn "no filter" into 0001-01-01, and a bound like
    /// <c>PostingDate &lt;= 0001-01-01</c> quietly empties the whole ledger. That regression is
    /// pinned by FinancialReportsApiTests.GeneralLedger_ReturnsPinnedShape_OldestRowAndFullSetTotals.
    /// </remarks>
    /// <param name="value">Raw query value, or null/whitespace when the caller omitted it.</param>
    /// <param name="date">Parsed date, or null when the parameter was absent.</param>
    private static bool TryParseDate(string? value, out DateOnly? date)
    {
        date = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!DateOnly.TryParse(
                value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return false;
        }

        date = parsed;
        return true;
    }

    private ObjectResult Problem400(string title, string detail, string? code)
        => Problem(StatusCodes.Status400BadRequest, title, detail, code);

    private ObjectResult Problem(int status, string title, string detail, string? code)
    {
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            Title = title,
            Status = status,
            Detail = detail,
            Instance = HttpContext.Request.Path.Value,
        };

        if (code is not null)
        {
            problem.Extensions["code"] = code;
        }

        return new ObjectResult(problem) { StatusCode = status };
    }
}
