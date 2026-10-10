using System;
using System.Collections.Generic;
using System.Linq;

namespace AssetHub.Domain.Exceptions;

/// <summary>
/// Input that failed validation (FluentValidation or hand-rolled guards).
/// The failure messages are catalog keys resolved at the API edge so that the
/// field errors come back in the culture of the request.
/// Mapped to HTTP 400 with an RFC 7807 body by ExceptionHandlingMiddleware.
/// </summary>
public class ValidationException : DomainException
{
    /// <summary>Field name → one or more localized message keys (or raw messages).</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationException()
        : base("validation_failed", "One or more validation errors occurred.")
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("validation_failed", "One or more validation errors occurred.")
    {
        Errors = errors ?? new Dictionary<string, string[]>();
    }

    public ValidationException(string field, string message)
        : base("validation_failed", message)
    {
        Errors = new Dictionary<string, string[]> { [field] = new[] { message } };
    }

    public bool HasErrors => Errors.Count > 0;

    public IEnumerable<string> AllMessages() => Errors.SelectMany(e => e.Value);
}
