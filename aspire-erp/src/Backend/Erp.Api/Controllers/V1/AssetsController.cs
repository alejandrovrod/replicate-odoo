using Erp.Api.Filters;
using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.Assets.Commands;
using Erp.Application.Features.Assets.Queries;
using Erp.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers.V1;

/// <summary>
/// Asset endpoints (Tasks 10.2/10.4/10.5): list and detail reads, the Draft/Submitted -&gt;
/// Capitalized transition, the periodic depreciation batch run and the Sold/Scrapped disposal.
/// Attributes follow Constitution Article VI: explicit route + versioning + TenantMember policy
/// (VI.1) and exhaustive status documentation (VI.3). The two ledger-posting mutations
/// (depreciation-run, dispose) carry the literal <c>[IdempotencyKeyRequired]</c> guard
/// (Article VI.4); capitalize posts its CWIP voucher through the same guard for replay safety.
/// </summary>
/// <remarks>
/// No asset-create endpoint exists by design: asset masters enter the books via SQL seeds (like
/// BOMs); the API starts at capitalization.
/// </remarks>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "TenantMember")]
[Produces("application/json")]
public sealed class AssetsController : ControllerBase
{
    private readonly ISender _sender;

    public AssetsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>Lists the company's asset headers. Read-only: no idempotency guard.</summary>
    /// <param name="companyId">Company that owns the assets.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AssetDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] Guid companyId,
        CancellationToken cancellationToken)
    {
        if (companyId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                "Invalid Company",
                "The companyId query parameter must be a non-empty GUID.",
                AssetErrorCodes.CompanyNotFound);
        }

        var assets = await _sender.SendAsync(new GetAssetsQuery(companyId), cancellationToken);
        return Ok(assets);
    }

    /// <summary>Returns one asset with its schedule lines ordered by due date. Read-only.</summary>
    /// <param name="id">Asset id.</param>
    /// <param name="companyId">Company that owns the asset.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AssetDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Detail(
        Guid id,
        [FromQuery] Guid companyId,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty || companyId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                "Invalid Asset",
                "Both the route id and the companyId query parameter must be non-empty GUIDs.",
                AssetErrorCodes.AssetNotFound);
        }

        var detail = await _sender.SendAsync(new GetAssetDetailQuery(companyId, id), cancellationToken);
        if (detail is null)
        {
            return Problem(
                StatusCodes.Status404NotFound,
                "Asset Not Found",
                $"Asset '{id}' was not found in this company.",
                AssetErrorCodes.AssetNotFound);
        }

        return Ok(detail);
    }

    /// <summary>
    /// Capitalizes a Draft/Submitted asset (spec AS-01): clears CWIP into the Fixed Asset account,
    /// generates the straight-line schedule and moves the asset to Capitalized.
    /// </summary>
    /// <remarks>
    /// Requires the <c>Idempotency-Key</c> header (Constitution VI.4): the capitalization posts a
    /// CWIP-clearing voucher, so a retried request must replay instead of double-posting.
    /// </remarks>
    /// <param name="id">Asset id.</param>
    /// <param name="request">Company and capitalization date.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost("{id:guid}/capitalize")]
    [IdempotencyKeyRequired]
    [ProducesResponseType(typeof(AssetCapitalizationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Capitalize(
        Guid id,
        [FromBody] CapitalizeAssetRequest request,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty || request.CompanyId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                "Invalid Asset",
                "Both the route id and the request companyId must be non-empty GUIDs.",
                AssetErrorCodes.AssetNotFound);
        }

        var result = await _sender.SendAsync(
            new CapitalizeAssetCommand(request.CompanyId, id, request.CapitalizationDate),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return AssetProblem(result.Error!);
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Runs the periodic depreciation batch (Task 10.4, spec AS-02/AS-04): books every due
    /// Scheduled line into one gapless DEP voucher and reports booked + skipped rows.
    /// </summary>
    /// <remarks>
    /// Requires the <c>Idempotency-Key</c> header (Constitution VI.4). A replayed key returns the
    /// stored response verbatim; a same-payload rerun without the key is additionally safe by
    /// construction (Booked rows are skipped, spec AS-04 - full 10.6 replay wiring stays Block C).
    /// </remarks>
    /// <param name="request">Company and as-of date.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost("depreciation-run")]
    [IdempotencyKeyRequired]
    [ProducesResponseType(typeof(DepreciationRunDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DepreciationRun(
        [FromBody] DepreciationRunRequest request,
        CancellationToken cancellationToken)
    {
        if (request.CompanyId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                "Invalid Company",
                "The request companyId must be a non-empty GUID.",
                AssetErrorCodes.CompanyNotFound);
        }

        var result = await _sender.SendAsync(
            new PostDueDepreciationsCommand(request.CompanyId, request.AsOfDate),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return AssetProblem(result.Error!);
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Disposes an asset by cash sale (via a company bank account) or $0 scrap (Task 10.5,
    /// spec AS-03/AS-05): clears cost and accrued depreciation, books the variance to the
    /// category's gain/loss account, cancels future schedule lines and moves the asset to
    /// Sold/Scrapped.
    /// </summary>
    /// <remarks>
    /// Requires the <c>Idempotency-Key</c> header (Constitution VI.4). Credit-sale disposal via
    /// Accounts Receivable is out of scope: proceeds travel via a bank account or not at all.
    /// </remarks>
    /// <param name="id">Asset id.</param>
    /// <param name="request">Company, disposal date, proceeds and optional bank account.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost("{id:guid}/dispose")]
    [IdempotencyKeyRequired]
    [ProducesResponseType(typeof(AssetDisposalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Dispose(
        Guid id,
        [FromBody] DisposeAssetRequest request,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty || request.CompanyId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                "Invalid Asset",
                "Both the route id and the request companyId must be non-empty GUIDs.",
                AssetErrorCodes.AssetNotFound);
        }

        var result = await _sender.SendAsync(
            new DisposeAssetCommand(
                request.CompanyId, id, request.DisposalDate,
                request.ProceedsAmount, request.ProceedsBankAccountId),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return AssetProblem(result.Error!);
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// RFC 7807 mapping for the asset posting routes, mirroring the manufacturing controller:
    /// missing masters are 404, state conflicts (bad transition, frozen period, concurrency
    /// race) are 409, every other domain failure - including the proceeds-shape and
    /// gain/loss-account rejections - is a 400 carrying the stable machine code.
    /// </summary>
    private ObjectResult AssetProblem(Error error) =>
        error.Code switch
        {
            AssetErrorCodes.AssetNotFound
                or AssetErrorCodes.CategoryNotFound
                or AssetErrorCodes.CompanyNotFound
                or AssetErrorCodes.ItemNotFound
                or AssetErrorCodes.BankAccountNotFound => Problem(
                    StatusCodes.Status404NotFound,
                    "Asset Resource Not Found",
                    error.Message,
                    error.Code),
            AssetErrorCodes.InvalidStatusTransition
                or ConcurrencyErrorCodes.ConcurrencyConflict
                or AccountingErrorCodes.FiscalPeriodLocked => Problem(
                    StatusCodes.Status409Conflict,
                    "Asset Conflict",
                    error.Message,
                    error.Code),
            _ => Problem(
                StatusCodes.Status400BadRequest,
                "Asset Rejected",
                error.Message,
                error.Code),
        };

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

/// <summary>Capitalization request body: the owning company and the accounting date.</summary>
public sealed record CapitalizeAssetRequest(
    Guid CompanyId,
    DateOnly CapitalizationDate);

/// <summary>Depreciation batch request body: the company and the as-of date.</summary>
public sealed record DepreciationRunRequest(
    Guid CompanyId,
    DateOnly AsOfDate);

/// <summary>
/// Disposal request body: the company, the accounting date, the cash proceeds (0 = scrap) and
/// the receiving bank account (required exactly when proceeds &gt; 0).
/// </summary>
public sealed record DisposeAssetRequest(
    Guid CompanyId,
    DateOnly DisposalDate,
    decimal ProceedsAmount,
    Guid? ProceedsBankAccountId = null);
