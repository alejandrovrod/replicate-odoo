namespace Erp.Domain.Exceptions;

/// <summary>
/// Raised when a Journal Entry invariant is violated (DDD: invariants throw). Carries the stable
/// <see cref="Code"/> from <c>Erp.Domain.Entities.JournalErrorCodes</c> so upper layers can map
/// the failure to an RFC 7807 response without string-matching messages - the exact shape of
/// <see cref="PurchaseValidationException"/> for the buying module.
/// </summary>
public sealed class JournalValidationException : Exception
{
    public string Code { get; }

    public JournalValidationException(string code, string message) : base(message)
    {
        Code = code;
    }
}
