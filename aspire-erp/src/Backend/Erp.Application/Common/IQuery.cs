namespace Erp.Application.Common;

/// <summary>Marker for a side-effect-free request producing a single <typeparamref name="TResult"/>.</summary>
/// <remarks>Hand-rolled CQRS - see <see cref="ICommand{TResult}"/> for the rationale (decision C2).</remarks>
public interface IQuery<TResult>
{
}

/// <summary>Handler for an <see cref="IQuery{TResult}"/>. Registered in Erp.Api's composition root.</summary>
public interface IQueryHandler<TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}
