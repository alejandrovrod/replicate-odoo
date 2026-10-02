using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Erp.Application.Common;

/// <summary>Entry point to dispatch commands and queries to their handlers.</summary>
public interface ISender
{
    Task<TResult> SendAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default);

    Task<TResult> SendAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Minimal hand-rolled dispatcher (decision C2 - no MediatR). Resolves the concrete handler from
/// the DI container and invokes it; the plan's <c>Behaviors/</c> pipeline can decorate
/// <see cref="ISender"/> later without touching call sites.
/// </summary>
/// <remarks>
/// Uses only the BCL <see cref="IServiceProvider"/> contract (NOT the
/// Microsoft.Extensions.DependencyInjection extension package) so Erp.Application keeps zero
/// NuGet references (Constitution I.3).
/// </remarks>
public sealed class Sender : ISender
{
    private readonly IServiceProvider _serviceProvider;

    public Sender(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task<TResult> SendAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default)
        => DispatchAsync<TResult>(
            typeof(ICommandHandler<,>).MakeGenericType(command.GetType(), typeof(TResult)),
            command,
            cancellationToken);

    public Task<TResult> SendAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default)
        => DispatchAsync<TResult>(
            typeof(IQueryHandler<,>).MakeGenericType(query.GetType(), typeof(TResult)),
            query,
            cancellationToken);

    private Task<TResult> DispatchAsync<TResult>(Type handlerType, object message, CancellationToken cancellationToken)
    {
        var handler = _serviceProvider.GetService(handlerType)
            ?? throw new InvalidOperationException(
                $"No handler is registered for '{handlerType.FullName}'. "
                + "Register it in Erp.Api's composition root (Program.cs).");

        var method = handlerType.GetMethod("HandleAsync", BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException($"Handler '{handlerType.FullName}' does not expose HandleAsync.");

        try
        {
            return (Task<TResult>)method.Invoke(handler, new object[] { message, cancellationToken })!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            // Preserve the original stack trace instead of surfacing the reflection wrapper.
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }
}
