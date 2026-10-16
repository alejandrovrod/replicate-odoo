namespace Erp.Domain.Exceptions;

/// <summary>
/// Raised when an <c>IDocumentLifecycleHook</c> aborts a Submit/Cancel and has no
/// domain-specific code of its own (extensibility-engine Fase 1, gap D3). Hooks with a
/// vertical vocabulary (e.g. <c>insurance_authorization_required</c>) SHOULD throw their own
/// coded exception instead - the handler propagates any exception's <c>Code</c> verbatim, and
/// this type is only the generic fallback carrying
/// <see cref="Entities.ExtensibilityErrorCodes.LifecycleHookRejected"/>.
/// </summary>
public sealed class LifecycleHookRejectedException : Exception
{
    public string Code { get; }

    public LifecycleHookRejectedException(string message)
        : base(message)
    {
        Code = Entities.ExtensibilityErrorCodes.LifecycleHookRejected;
    }

    public LifecycleHookRejectedException(string code, string message) : base(message)
    {
        Code = code;
    }
}
