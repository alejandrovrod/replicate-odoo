namespace Erp.Domain.Common;

/// <summary>
/// Open pipeline for satellite modules and industry verticals (extensibility-engine Fase 1,
/// gap D3): validations and side effects around a document's Submit/Cancel without modifying
/// the Core aggregate. Implementations are resolved as <c>IEnumerable{IDocumentLifecycleHook{T}}</c>
/// and run ordered by <see cref="Order"/> ascending.
/// </summary>
/// <remarks>
/// <para>Composition over conditionals: a health vertical registers
/// <c>IDocumentLifecycleHook&lt;SalesInvoice&gt;</c> checking insurance authorization instead of
/// the Core learning about insurance. The Core stays industry-agnostic.</para>
/// <para>Abort contract: throw a domain exception carrying a stable <c>Code</c> (e.g.
/// <c>CustomerValidationException</c> or <c>LifecycleHookRejectedException</c>). The handler
/// maps the code to RFC 7807 and - for Before hooks running outside the transaction - no
/// ledger row is written.</para>
/// </remarks>
public interface IDocumentLifecycleHook<in TDocument> where TDocument : class
{
    /// <summary>Execution order, ascending. Default 0 (registration order decides ties).</summary>
    int Order => 0;

    Task OnBeforeSubmitAsync(TDocument document, CancellationToken ct);

    Task OnAfterSubmitAsync(TDocument document, CancellationToken ct);

    Task OnBeforeCancelAsync(TDocument document, CancellationToken ct);

    Task OnAfterCancelAsync(TDocument document, CancellationToken ct);
}
