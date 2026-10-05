using Erp.Api.Common;
using Erp.Api.Filters;
using Erp.Api.Localization;
using Erp.Api.Shared;
using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.Banking.Commands;
using Erp.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Erp.Api.Controllers.V1;

/// <summary>
/// Statement import endpoints (tasks 6.2 import + Block B exposure): the JSON-body import that
/// stages one file's lines as Unreconciled <see cref="BankTransaction"/> rows with zero
/// <c>GLEntry</c> writes (invariant BN-01). Attributes follow Constitution Article VI:
/// explicit route + versioning + TenantMember policy (VI.1), JSON content negotiation (VI.2)
/// and exhaustive status documentation (VI.3).
/// </summary>
[ApiController]
[Route("api/v1/bank-statement-imports")]
[Authorize(Policy = "TenantMember")]
[Produces("application/json")]
[Consumes("application/json")]
public sealed class BankStatementImportsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IStringLocalizer<ErrorMessages> _errors;
    private readonly IStringLocalizer<CommonMessages> _common;

    public BankStatementImportsController(
        ISender sender,
        IStringLocalizer<ErrorMessages> errors,
        IStringLocalizer<CommonMessages> common)
    {
        _sender = sender;
        _errors = errors;
        _common = common;
    }

    /// <summary>Import request: the statement file travels as inline text content.</summary>
    /// <param name="CompanyId">Company that owns the bank account.</param>
    /// <param name="BankAccountId">Target bank account of the import.</param>
    /// <param name="FileName">Uploaded file name recorded on the batch header.</param>
    /// <param name="Format">Statement format: "CSV" or "OFX" (case-insensitive).</param>
    /// <param name="Content">Raw file content (CSV text or OFX 1.x SGML).</param>
    public sealed record ImportBankStatementRequest(
        Guid CompanyId,
        Guid BankAccountId,
        string FileName,
        string Format,
        string Content);

    /// <summary>
    /// Imports one statement file into isolated staging: the batch header plus one
    /// Unreconciled staging row per new line, inside ONE transaction.
    /// </summary>
    /// <remarks>
    /// Requires the <c>Idempotency-Key</c> header (Constitution VI.4): re-importing the same
    /// file is a no-op summary by FITID de-duplication (scenario BN-05), and the filter
    /// additionally guards double submission at the HTTP layer.
    /// </remarks>
    [HttpPost]
    [IdempotencyKeyRequired]
    [ProducesResponseType(typeof(BankStatementImportDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Import(
        [FromBody] ImportBankStatementRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(
            new ImportBankStatementCommand(
                request.CompanyId,
                request.BankAccountId,
                request.FileName,
                request.Content,
                request.Format),
            cancellationToken);

        if (!result.IsSuccess)
        {
            var error = result.Error!;
            return error.Code switch
            {
                BankingErrorCodes.BankAccountNotFound => Problem(
                    StatusCodes.Status404NotFound,
                    _common.Text("BankAccountNotFound"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
                _ => Problem(
                    StatusCodes.Status400BadRequest,
                    _common.Text("StatementImportRejected"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
            };
        }

        var summary = result.Value!;
        return CreatedAtAction(
            nameof(Import),
            new { },
            new BankStatementImportDto(
                summary.ImportId,
                summary.FileName,
                summary.TotalTransactions,
                summary.ImportedCount,
                summary.DuplicateCount));
    }

    private ObjectResult Problem(int status, string title, string detail, string? code)
    {
        var problem = new ProblemDetails
        {
            Type = status switch
            {
                StatusCodes.Status404NotFound => "https://tools.ietf.org/html/rfc9110#section-15.5.5",
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
