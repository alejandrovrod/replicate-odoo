using System;

namespace AssetHub.Domain.Exceptions;

/// <summary>
/// Business-rule violation raised anywhere in the domain/application layers.
///
/// The exception deliberately carries a <see cref="ResourceKey"/> instead of a
/// localized sentence: the Domain project has no reference to the message
/// catalog (nor should it). Resolution happens at the API edge, in
/// ExceptionHandlingMiddleware, against the culture of the incoming request
/// (Accept-Language). <c>Message</c> is retained only as a fallback for keys
/// that have not been added to the catalog yet.
/// </summary>
public class DomainException : Exception
{
    public string Code { get; }

    /// <summary>
    /// Key inside SharedResource. Defaults to <see cref="Code"/>, so the error code
    /// doubles as the localization key and most throw sites need no extra plumbing.
    /// Explicitly set only when one code needs more than one wording or argument list.
    /// </summary>
    public string? ResourceKey { get; }

    /// <summary>Positional arguments for the catalog entry ({0}, {1}, ...).</summary>
    public object[] Args { get; }

    public DomainException(string code, string message) : base(message)
    {
        Code = code;
        ResourceKey = code;
        Args = Array.Empty<object>();
    }

    public DomainException(string code, string message, string resourceKey, params object[] args) : base(message)
    {
        Code = code;
        ResourceKey = resourceKey;
        Args = args ?? Array.Empty<object>();
    }
}
