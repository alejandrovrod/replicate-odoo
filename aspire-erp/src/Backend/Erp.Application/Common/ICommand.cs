namespace Erp.Application.Common;

/// <summary>Marker for a state-changing request producing a single <typeparamref name="TResult"/>.</summary>
/// <remarks>
/// Hand-rolled CQRS (decision C2): MediatR 13+ is commercially licensed and no task requires it,
/// so Erp.Application keeps its ONLY dependency on Erp.Domain (Constitution I.3, zero NuGet packages).
/// </remarks>
public interface ICommand<TResult>
{
}

/// <summary>Handler for an <see cref="ICommand{TResult}"/>. Registered in Erp.Api's composition root.</summary>
public interface ICommandHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}
