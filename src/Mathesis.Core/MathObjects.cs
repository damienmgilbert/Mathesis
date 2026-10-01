namespace Mathesis;

/// <summary>
/// Marker for mathematical objects that engines pass around without <c>Mathesis.Core</c> knowing their concrete type,
/// such as <c>Expr</c> (defined in <c>Mathesis.Symbolics</c>).
/// </summary>
/// <remarks>
/// Core cannot reference Symbolics, yet <see cref="Outcome{T}"/> and <see cref="Provisos"/> must carry expressions and
/// derivations. Symbolics makes <c>Expr</c> implement this interface and <c>Derivation</c> implement
/// <see cref="IDerivation"/>; consumers cast back to the concrete type.
/// </remarks>
public interface IMathObject
{
}

/// <summary>Marker for a recorded derivation (a tree of steps); implemented by <c>Derivation</c> in <c>Mathesis.Symbolics</c>.</summary>
public interface IDerivation : IMathObject
{
}
