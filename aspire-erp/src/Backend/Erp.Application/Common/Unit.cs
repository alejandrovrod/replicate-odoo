namespace Erp.Application.Common;

/// <summary>
/// Void-equivalent for commands that report only success/failure through
/// <see cref="Result{T}"/> (e.g. <c>Result&lt;Unit&gt;</c>). Mirrors the MediatR Unit shape
/// without taking the MediatR dependency (decision C2 - MediatR 13+ is commercially licensed).
/// </summary>
public readonly struct Unit : IEquatable<Unit>
{
    public static readonly Unit Value = new();

    public bool Equals(Unit other) => true;
    public override bool Equals(object? obj) => obj is Unit;
    public override int GetHashCode() => 0;
    public override string ToString() => "()";
}
