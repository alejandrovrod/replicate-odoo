using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.Buying.Commands;
using Erp.Application.Features.Buying.Queries;
using Erp.Api.Filters;
using Erp.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers.V1;

/// <summary>
/// Purchase invoice endpoints (Task 4.3 / spec BY-01): POST creates AND posts the vendor bill in
/// one transaction - Dr Stock Received But Not Billed at receipt value + Dr Input Tax Recoverable +
/// Dr/Cr price difference / Cr Accounts Payable, gapless PINV voucher and the order's Billed
/// transition. One invoice per receipt (full three-way match v1). The POST carries the literal
/// <c>[IdempotencyKeyRequired]</c> guard demanded by Article VI.4 because it writes GLEntry.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "TenantMember")]
[Produces("application/json")]
[Consumes("application/json")]
public sealed class PurchaseInvoicesController : ControllerBase
{
    private readonly ISender _sender;

    public PurchaseInvoicesController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>Returns the company's most recent purchase invoices with their lines.</summary>
    /// <param name="companyId">Company that owns the invoices.</param>
    /// <param name="limit">Maximum number of invoices to return (defaults to 50).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PurchaseInvoiceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Get(
        [FromQuery] Guid companyId,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (companyId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                "Invalid Company",
                "The companyId query parameter must be a non-empty GUID.",
                PurchaseErrorCodes.CompanyNotFound);
        }

        var invoices = await _sender.SendAsync(new GetPurchaseInvoicesQuery(companyId, limit), cancellationToken);
        return Ok(invoices);
    }

    /// <summary>
    /// Creates AND posts one purchase invoice in a single transaction: interim liability clearance,
    /// input tax and price difference against a balanced Accounts Payable credit (spec BY-01).
    /// </summary>
    /// <remarks>
    /// Requires the <c>Idempotency-Key</c> header (Constitution VI.4). A missing header is a 400, a
    /// replayed key returns the stored response verbatim, and reusing a key with a different payload
    /// is a 409. A receipt already invoiced is also a 409 (<c>invoice_already_exists</c>), and the
    /// full three-way match rejects quantity/coverage mismatches with 400.
    /// </remarks>
    /// <param name="command">Invoice data (company, receipt, posting date, tax, lines).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost]
    [IdempotencyKeyRequired]
    [ProducesResponseType(typeof(PurchaseInvoicePostingDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] PostPurchaseInvoiceCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(command, cancellationToken);

        if (!result.IsSuccess)
        {
            var error = result.Error!;

            // RFC 7807: the one-invoice-per-receipt rule conflicts with the existing bill, every
            // other domain rejection (three-way match included) is a bad request.
            return error.Code switch
            {
                PurchaseErrorCodes.InvoiceAlreadyExists => Problem(
                    StatusCodes.Status409Conflict,
                    "Duplicate Purchase Invoice",
                    error.Message,
                    error.Code),
                PurchaseErrorCodes.InvalidStatusTransition => Problem(
                    StatusCodes.Status409Conflict,
                    "Purchase Order Conflict",
                    error.Message,
                    error.Code),
                _ => Problem(
                    StatusCodes.Status400BadRequest,
                    "Purchase Invoice Rejected",
                    error.Message,
                    error.Code),
            };
        }

        var posting = result.Value!;
        return CreatedAtAction(nameof(Get), new { companyId = posting.Invoice.CompanyId }, posting);
    }

    private ObjectResult Problem(int status, string title, string detail, string? code)
    {
        var problem = new ProblemDetails
        {
            Type = status switch
            {
                StatusCodes.Status409Conflict => "https://tools.ietf.org/html/rfc9110#section-15.5.10",
                _ => "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            },
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
