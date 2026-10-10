using Erp.Api.Common;
using Erp.Api.Filters;
using Erp.Api.Localization;
using Erp.Api.Shared;
using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.Assets.Commands;
using Erp.Application.Features.Assets.Queries;
using Erp.Domain.Common;
using Erp.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Erp.Api.Controllers.V1;

/// <summary>
/// Asset endpoints (Tasks 10.2/10.4/10.5/10.6): list and detail reads, the Draft/Submitted ->
/// Capitalized transition, the periodic depreciation batch run, the Sold/Scrapped disposal,
/// and the disposal reversal.
/// Attributes follow Constitution Article VI: explicit route + versioning + TenantMember policy
/// (VI.1) and exhaustive status documentation (VI.3). The ledger-posting mutations
/// (depreciation-run, dispose, reverse-disposal) carry the literal <c>[IdempotencyKeyRequired]</c> guard
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
    private readonly IStringLocalizer<ErrorMessages> _errors;
    private readonly IStringLocalizer<CommonMessages> _common;

    public AssetsController(
        ISender sender,
        IStringLocalizer<ErrorMessages> errors,
        IStringLocalizer<CommonMessages> common)
    {
        _sender = sender;
        _errors = errors;
        _common = common;
    }

    /// <summary>Lists the company's asset headers. Read-only: no idempotency guard.</summary>
    /// <param name="companyId">Company that owns the assets.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet]
    [Authorize(Policy = "permission:asset:read")]
    [ProducesResponseType(typeof(PagedResult<AssetDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
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
                _errors.Text(AssetErrorCodes.CompanyNotFound),
                AssetErrorCodes.CompanyNotFound);
        }

        var assets = await _sender.SendAsync(new GetAssetsQuery(companyId, page, pageSize), cancellationToken);
        return Ok(assets);
    }

    /// <summary>Returns one asset with its schedule lines ordered by due date. Read-only.</summary>
    /// <param name="id">Asset id.</param>
    /// <param name="companyId">Company that owns the asset.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "permission:asset:read")]
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
                _common.Text("InvalidAsset"),
                _errors.Text(AssetErrorCodes.AssetNotFound),
                AssetErrorCodes.AssetNotFound);
        }

        var detail = await _sender.SendAsync(new GetAssetDetailQuery(companyId, id), cancellationToken);
        if (detail is null)
        {
            return Problem(
                StatusCodes.Status404NotFound,
                _common.Text("AssetNotFound"),
                // Instance-valued detail (asset id): passes through untranslated while the title
                // still localizes (Phase 2 convention).
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
    [Authorize(Policy = "permission:asset:submit")]
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
                _common.Text("InvalidAsset"),
                _errors.Text(AssetErrorCodes.AssetNotFound),
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
    [Authorize(Policy = "permission:asset:submit")]
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
    [Authorize(Policy = "permission:asset:submit")]
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
                _common.Text("InvalidAsset"),
                _errors.Text(AssetErrorCodes.AssetNotFound),
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
    /// Reverses an asset disposal (Task 10.6, spec AS-05 reversal): undoes the disposal GL
    /// voucher, reopens cancelled schedule lines, restores AccumulatedDepreciation and the
    /// asset's Capitalized/FullyDepreciated status, and clears DisposalDate.
    /// </summary>
    /// <remarks>
    /// Requires the <c>Idempotency-Key</c> header (Constitution VI.4). Only Sold/Scrapped
    /// assets can be reversed; an already-reversed asset fails with <c>already_reversed</c>.
    /// The reversal voucher (RDS-YYYY-NNNNN) mirrors the original disposal GL exactly.
    /// </remarks>
    /// <param name="id">Asset id.</param>
    /// <param name="request">Company, optional posting date (defaults to disposal date), and RowVersion for concurrency.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost("{id:guid}/reverse-disposal")]
    [Authorize(Policy = "permission:asset:cancel")]
    [IdempotencyKeyRequired]
    [ProducesResponseType(typeof(AssetDisposalReversalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReverseDisposal(
        Guid id,
        [FromBody] CancelDisposeAssetRequest request,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty || request.CompanyId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("InvalidAsset"),
                _errors.Text(AssetErrorCodes.AssetNotFound),
                AssetErrorCodes.AssetNotFound);
        }

        var result = await _sender.SendAsync(
            new CancelDisposeAssetCommand(
                request.CompanyId, id, request.PostingDate, request.RowVersion),
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
                    _common.Text("AssetResourceNotFound"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
            AssetErrorCodes.InvalidStatusTransition
                or AssetErrorCodes.AssetNotDisposed
                or AssetErrorCodes.AlreadyReversed
                or ConcurrencyErrorCodes.ConcurrencyConflict
                or AccountingErrorCodes.FiscalPeriodLocked => Problem(
                    StatusCodes.Status409Conflict,
                    _common.Text("AssetConflict"),
                    error.Code switch
                    {
                        // Instance-valued detail (period dates) passes through (Phase 2 convention).
                        AccountingErrorCodes.FiscalPeriodLocked => error.Message,
                        _ => _errors.Text(error.Code, error.Message),
                    },
                    error.Code),
            _ => Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("AssetRejected"),
                _errors.Text(error.Code, error.Message),
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
/// the receiving bank account (required exactly when proceeds > 0).
/// </summary>
public sealed record DisposeAssetRequest(
    Guid CompanyId,
    DateOnly DisposalDate,
    decimal ProceedsAmount,
    Guid? ProceedsBankAccountId = null);

/// <summary>
/// Disposal reversal request body: the company, optional posting date (defaults to disposal date),
/// and the RowVersion for optimistic concurrency (required to prevent lost updates per spec AS-06).
/// </summary>
public sealed record CancelDisposeAssetRequest(
    Guid CompanyId,
    DateOnly? PostingDate = null,
    byte[]? RowVersion = null);
