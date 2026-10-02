using System.Security.Cryptography;
using System.Text;
using Erp.Application.Common;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Erp.Api.Filters;

/// <summary>
/// The idempotency guard behind <see cref="IdempotencyKeyRequiredAttribute"/> (Constitution VI.4 /
/// decision D7).
/// </summary>
/// <remarks>
/// <para>
/// Implemented as a RESOURCE filter because it must hash the RAW request body, and resource
/// filters run before model binding (ASP.NET Core filter pipeline: "OnResourceExecuting runs code
/// before model binding") - an action filter would find the body already consumed by
/// <c>[FromBody]</c> binding.
/// </para>
/// <para>
/// Flow: header check (missing/empty -> 400 ProblemDetails) -> SHA-256 of the raw body ->
/// classification via <see cref="IdempotencyPolicy"/> (replay 2xx verbatim / 409 in progress /
/// 409 key reuse with a different payload) -> atomic reservation -> run the action while capturing
/// the response body -> 2xx stores status+body, anything else DELETES the reservation so the client
/// can legitimately retry the same key.
/// </para>
/// <para>
/// CRASH-WINDOW TRADE-OFF: a crash (or process kill) between the reservation INSERT and the
/// response capture leaves the key PENDING forever, and honest retries then get 409 "in progress"
/// until the row is cleaned up. Accepted for Phase 3 because the alternative - auto-expiring
/// pending rows by age - would let a genuinely slow request run twice. The mitigation is a simple
/// operational DELETE of stale pending rows (CreatedAt older than the longest plausible request),
/// which is safe: the row carries no business state, only the reservation.
/// </para>
/// </remarks>
public sealed class IdempotencyFilter : IAsyncResourceFilter
{
    public const string HeaderName = "Idempotency-Key";

    private readonly IIdempotencyRepository _idempotency;

    public IdempotencyFilter(IIdempotencyRepository idempotency)
    {
        _idempotency = idempotency;
    }

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var http = context.HttpContext;
        var cancellationToken = http.RequestAborted;

        if (!http.Request.Headers.TryGetValue(HeaderName, out var rawValues)
            || !IdempotencyPolicy.HasUsableKey(rawValues.ToString()))
        {
            await WriteProblemAsync(
                http,
                StatusCodes.Status400BadRequest,
                "idempotency_key_required",
                "Idempotency Key Required",
                $"The {HeaderName} header must be a non-empty string on ledger-posting mutations "
                + "(Constitution VI.4).",
                cancellationToken);
            context.Result = new EmptyResult();
            return;
        }

        var key = rawValues.ToString().Trim();

        // RESOURCE filter => model binding has not read the body yet: buffer it, hash it, rewind
        // so [FromBody] binding still sees the complete payload.
        var requestHash = await ReadBodyHashAsync(http, cancellationToken);

        var (decision, stored) = await ClassifyAsync(key, requestHash, cancellationToken);
        if (decision != IdempotencyDecision.Proceed)
        {
            await ShortCircuitAsync(http, decision, key, requestHash, stored, cancellationToken);
            context.Result = new EmptyResult();
            return;
        }

        // Atomic reservation: the UNIQUE (TenantId, Key) index decides the winner between
        // concurrent identical requests (TryReserveAsync owns the INSERT).
        var reserved = await _idempotency.TryReserveAsync(key, requestHash, cancellationToken);
        if (!reserved)
        {
            var (raceDecision, raceStored) = await ClassifyAsync(key, requestHash, cancellationToken);
            if (raceDecision == IdempotencyDecision.Proceed)
            {
                // The winner vanished between INSERT and read (released after its own failure):
                // treat as in progress - the client retries and gets the stored response later.
                raceDecision = IdempotencyDecision.RequestInProgress;
            }

            await ShortCircuitAsync(http, raceDecision, key, requestHash, raceStored, cancellationToken);
            context.Result = new EmptyResult();
            return;
        }

        // Capture the action's response so a 2xx can be replayed byte-for-byte.
        //
        // Swapping Response.Body works for BOTH write paths: DefaultHttpResponse.Body's setter
        // installs a StreamResponseBodyFeature around the new stream, and BodyWriter is resolved
        // from that same feature - so SystemTextJsonOutputFormatter (which serialises through
        // BodyWriter) and any direct Stream write both land in `capture`. Restoring the original
        // stream reverts the whole feature (verified against the framework's revert branch).
        var originalBody = http.Response.Body;
        using var capture = new MemoryStream();
        http.Response.Body = capture;

        try
        {
            await next();

            // Drain the adapted PipeWriter created over `capture` so no bytes are stranded in it.
            await http.Response.BodyWriter.FlushAsync(cancellationToken);
        }
        catch
        {
            http.Response.Body = originalBody;

            // Exception => no usable response was produced: release the key so the client may retry.
            await _idempotency.ReleaseAsync(key, cancellationToken);
            throw;
        }

        var bytes = capture.ToArray();

        // Hand the captured payload to the real response stream: everything the action wrote went
        // into `capture`, so without this the client would receive an EMPTY body.
        http.Response.Body = originalBody;
        if (bytes.Length > 0)
        {
            await originalBody.WriteAsync(bytes, cancellationToken);
        }

        var responseBody = bytes.Length == 0
            ? string.Empty
            : Encoding.UTF8.GetString(bytes);

        var statusCode = http.Response.StatusCode;
        if (statusCode is >= 200 and <= 299)
        {
            await _idempotency.CompleteAsync(key, statusCode, responseBody, cancellationToken);
        }
        else
        {
            // 4xx/5xx: delete the reservation - the client is allowed to retry this key for real.
            await _idempotency.ReleaseAsync(key, cancellationToken);
        }
    }

    private async Task<(IdempotencyDecision Decision, IdempotencyRecord? Stored)> ClassifyAsync(
        string key,
        string requestHash,
        CancellationToken cancellationToken)
    {
        var existing = await _idempotency.GetByKeyAsync(key, cancellationToken);
        var decision = IdempotencyPolicy.Decide(
            existing is not null,
            existing?.RequestHash ?? string.Empty,
            existing?.ResponseStatus,
            requestHash);

        return (decision, existing);
    }

    private static async Task ShortCircuitAsync(
        HttpContext http,
        IdempotencyDecision decision,
        string key,
        string requestHash,
        IdempotencyRecord? stored,
        CancellationToken cancellationToken)
    {
        switch (decision)
        {
            case IdempotencyDecision.ReplayStoredResponse:
                http.Response.StatusCode = stored!.ResponseStatus!.Value;
                http.Response.ContentType = "application/json";
                var bytes = Encoding.UTF8.GetBytes(stored.ResponseBody ?? string.Empty);
                await http.Response.Body.WriteAsync(bytes, cancellationToken);
                break;

            case IdempotencyDecision.RequestInProgress:
                await WriteProblemAsync(
                    http,
                    StatusCodes.Status409Conflict,
                    "idempotency_request_in_progress",
                    "Idempotent Request In Progress",
                    $"A request with Idempotency-Key '{key}' is still being processed. Retry in a moment.",
                    cancellationToken);
                break;

            case IdempotencyDecision.KeyReuseWithDifferentPayload:
                await WriteProblemAsync(
                    http,
                    StatusCodes.Status409Conflict,
                    "idempotency_key_reused",
                    "Idempotency Key Reused",
                    $"Idempotency-Key '{key}' was already used with a DIFFERENT request payload "
                    + $"(stored hash {stored?.RequestHash?[..12]}... vs incoming {requestHash[..12]}...). "
                    + "Generate a new key for a new request.",
                    cancellationToken);
                break;

            default:
                throw new InvalidOperationException($"Unexpected idempotency decision '{decision}'.");
        }
    }

    private static async Task<string> ReadBodyHashAsync(HttpContext http, CancellationToken cancellationToken)
    {
        http.Request.EnableBuffering();
        http.Request.Body.Position = 0;

        string body;
        using (var reader = new StreamReader(http.Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true))
        {
            body = await reader.ReadToEndAsync(cancellationToken);
        }

        // Rewind so MVC's [FromBody] model binding (which runs AFTER this filter) reads the body.
        http.Request.Body.Position = 0;

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(body));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static async Task WriteProblemAsync(
        HttpContext http,
        int status,
        string code,
        string title,
        string detail,
        CancellationToken cancellationToken = default)
    {
        http.Response.StatusCode = status;

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
            Instance = http.Request.Path.Value,
        };
        problem.Extensions["code"] = code;

        await http.Response.WriteAsJsonAsync(
            problem,
            options: null,
            contentType: "application/problem+json",
            cancellationToken: cancellationToken);
    }
}
