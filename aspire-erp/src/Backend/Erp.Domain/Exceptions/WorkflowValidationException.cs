namespace Erp.Domain.Exceptions;

/// <summary>
/// Raised when an operational workflow invariant is violated (Fase 5). Carries the stable
/// <see cref="Code"/> from <see cref="Entities.WorkflowErrorCodes"/> so upper layers map
/// the failure to an RFC 7807 response without string-matching messages - the exact shape
/// of <see cref="StockValidationException"/>.
/// </summary>
public sealed class WorkflowValidationException : Exception
{
    public string Code { get; }

    public WorkflowValidationException(string code, string message) : base(message)
    {
        Code = code;
    }
}
