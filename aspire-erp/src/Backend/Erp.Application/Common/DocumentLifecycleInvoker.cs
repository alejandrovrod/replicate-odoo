using Erp.Domain.Common;

namespace Erp.Application.Common;

/// <summary>
/// Ordered execution of <see cref="IDocumentLifecycleHook{TDocument}"/> around a document's
/// Submit/Cancel (extensibility-engine Fase 1, gap D3). Pure orchestration: hooks run by
/// ascending <c>Order</c>; the FIRST throwing hook aborts the pipeline and its coded
/// exception propagates to the handler, which maps the code to RFC 7807.
/// </summary>
/// <remarks>
/// Both Before and After hooks run INSIDE the posting transaction (Before right after the
/// document is loaded, After after the ledger rows are staged but before commit): any hook
/// failure rolls back the whole operation, so a rejection (EXT-G3) always means zero GL
/// rows and zero half-states. There is deliberately no "commit then notify" split in Fase 1.
/// </remarks>
public static class DocumentLifecycleInvoker
{
    public static async Task RunBeforeSubmitAsync<TDocument>(
        IEnumerable<IDocumentLifecycleHook<TDocument>>? hooks,
        TDocument document,
        CancellationToken cancellationToken = default)
        where TDocument : class
    {
        if (hooks is null)
        {
            return;
        }

        foreach (var hook in hooks.OrderBy(h => h.Order))
        {
            await hook.OnBeforeSubmitAsync(document, cancellationToken);
        }
    }

    public static async Task RunAfterSubmitAsync<TDocument>(
        IEnumerable<IDocumentLifecycleHook<TDocument>>? hooks,
        TDocument document,
        CancellationToken cancellationToken = default)
        where TDocument : class
    {
        if (hooks is null)
        {
            return;
        }

        foreach (var hook in hooks.OrderBy(h => h.Order))
        {
            await hook.OnAfterSubmitAsync(document, cancellationToken);
        }
    }

    public static async Task RunBeforeCancelAsync<TDocument>(
        IEnumerable<IDocumentLifecycleHook<TDocument>>? hooks,
        TDocument document,
        CancellationToken cancellationToken = default)
        where TDocument : class
    {
        if (hooks is null)
        {
            return;
        }

        foreach (var hook in hooks.OrderBy(h => h.Order))
        {
            await hook.OnBeforeCancelAsync(document, cancellationToken);
        }
    }

    public static async Task RunAfterCancelAsync<TDocument>(
        IEnumerable<IDocumentLifecycleHook<TDocument>>? hooks,
        TDocument document,
        CancellationToken cancellationToken = default)
        where TDocument : class
    {
        if (hooks is null)
        {
            return;
        }

        foreach (var hook in hooks.OrderBy(h => h.Order))
        {
            await hook.OnAfterCancelAsync(document, cancellationToken);
        }
    }
}
