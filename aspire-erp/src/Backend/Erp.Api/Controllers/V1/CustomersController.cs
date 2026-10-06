using Erp.Api.Common;
using Erp.Api.Localization;
using Erp.Api.Shared;
using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.Selling.Commands;
using Erp.Application.Features.Selling.Queries;
using Erp.Domain.Common;
using Erp.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Erp.Api.Controllers.V1;

/// <summary>
/// Customer master endpoints (Task 5.1: codes unique per COMPANY - plan.md §1
/// UQ_Customer_Tenant_Company_Code). Route resolves to <c>/api/v1/customers</c> (single word, no
/// kebab-case alias needed). No <c>[IdempotencyKeyRequired]</c> here: Article VI.4 scopes that
/// guard to ledger-posting mutations and creating a customer writes no GLEntry row (same reasoning
/// as SuppliersController / ItemsController).
/// </summary>
/// <remarks>
/// <para><b>Status mapping</b> (the established pipeline): <c>duplicate_customer_code</c> conflicts
/// with the existing master -&gt; 409; <c>customer_not_found</c> -&gt; 404; everything else
/// (<c>customer_code_required</c>, <c>invalid_credit_limit</c>, ...) describes a bad REQUEST -&gt;
/// 400.</para>
/// </remarks>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "TenantMember")]
[Produces("application/json")]
[Consumes("application/json")]
public sealed class CustomersController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IStringLocalizer<ErrorMessages> _errors;
    private readonly IStringLocalizer<CommonMessages> _common;

    public CustomersController(
        ISender sender,
        IStringLocalizer<ErrorMessages> errors,
        IStringLocalizer<CommonMessages> common)
    {
        _sender = sender;
        _errors = errors;
        _common = common;
    }

    /// <summary>Returns the company's most recent customers.</summary>
    /// <param name="companyId">Company that owns the customers (plan.md §1 CompanyId NOT NULL).</param>
    /// <param name="limit">Maximum number of customers to return (defaults to 50).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<CustomerDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Get(
        [FromQuery] Guid companyId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (companyId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("InvalidCompany"),
                _errors.Text(SellingErrorCodes.CompanyRequired),
                SellingErrorCodes.CompanyRequired);
        }

        var customers = await _sender.SendAsync(new GetCustomersQuery(companyId, page, pageSize), cancellationToken);
        return Ok(customers);
    }

    /// <summary>Returns one customer by id.</summary>
    /// <param name="id">Customer id.</param>
    /// <param name="companyId">Company that owns the customer.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        Guid id,
        [FromQuery] Guid companyId,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty || companyId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("InvalidCustomer"),
                _errors.Text(SellingErrorCodes.CustomerNotFound),
                SellingErrorCodes.CustomerNotFound);
        }

        var customer = await _sender.SendAsync(new GetCustomerByIdQuery(companyId, id), cancellationToken);

        return customer is null
            ? Problem(
                StatusCodes.Status404NotFound,
                _common.Text("CustomerNotFound"),
                _errors.Text(SellingErrorCodes.CustomerNotFound),
                SellingErrorCodes.CustomerNotFound)
            : Ok(customer);
    }

    /// <summary>Creates one customer (unique code per company - duplicates are a 409).</summary>
    /// <param name="command">Customer data (company, code, name, tax id, credit terms).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateCustomerCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(command, cancellationToken);

        if (!result.IsSuccess)
        {
            var error = result.Error!;

            // RFC 7807: a duplicate code conflicts with the existing master, anything else is a
            // field/lookup rejection carrying the stable machine code.
            return error.Code switch
            {
                SellingErrorCodes.DuplicateCustomerCode => Problem(
                    StatusCodes.Status409Conflict,
                    _common.Text("DuplicateCustomerCode"),
                    // Instance-valued detail (the colliding code, pinned by
                    // CustomersApiTests): passes through untranslated, the title still
                    // localizes. See the Phase 2 convention in tasks.md.
                    error.Message,
                    error.Code),
                _ => Problem(
                    StatusCodes.Status400BadRequest,
                    _common.Text("CustomerRejected"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
            };
        }

        var dto = result.Value!;
        return CreatedAtAction(nameof(GetById), new { id = dto.Id, companyId = dto.CompanyId }, dto);
    }

    private ObjectResult Problem(int status, string title, string detail, string? code)
    {
        var problem = new ProblemDetails
        {
            Type = status switch
            {
                StatusCodes.Status409Conflict => "https://tools.ietf.org/html/rfc9110#section-15.5.10",
                StatusCodes.Status404NotFound => "https://tools.ietf.org/html/rfc9110#section-15.5.5",
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
