namespace Erp.Application.Common;

/// <summary>Stable machine-readable failure code + human message (RFC 7807 payload ingredients).</summary>
public sealed record Error(string Code, string Message);

/// <summary>
/// Outcome wrapper so handlers can report domain failures (e.g. duplicate AccountCode) without
/// throwing across layers; Erp.Api maps <see cref="Error"/>.Code to 400/409 ProblemDetails.
/// </summary>
public sealed class Result<T>
{
    public bool IsSuccess { get; }

    /// <summary>Valid when <see cref="IsSuccess"/> is true; default otherwise.</summary>
    public T? Value { get; }

    /// <summary>Valid when <see cref="IsSuccess"/> is false.</summary>
    public Error? Error { get; }

    private Result(bool isSuccess, T? value, Error? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public static Result<T> Success(T value) => new(true, value, null);

    public static Result<T> Failure(string code, string message) => new(false, default, new Error(code, message));

    public static Result<T> Failure(Error error) => new(false, default, error);
}
