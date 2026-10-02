using Mathesis.Symbolics.Assumptions;
using Mathesis.Symbolics.Canonical;

namespace Mathesis.Symbolics;

/// <summary>
/// The setting an operation runs in: the number field and the assumptions about symbols
/// (docs/design/03-namespaces-and-packages.md, "A first program").
/// </summary>
public sealed record MathContext
{
    /// <summary>Real mode with no assumptions.</summary>
    public static MathContext Default { get; } = new();

    /// <summary>The number field; real mode by default, with <c>I</c> as the imaginary unit available.</summary>
    public NumberField Field { get; init; } = NumberField.Real;

    /// <summary>The facts about symbols.</summary>
    public AssumptionSet Assumptions { get; init; } = AssumptionSet.Empty;

    /// <summary>The normalization options that match <see cref="Field"/>.</summary>
    public NormalizeOptions NormalizeOptions => new() { Field = Field };

    /// <summary>A context with <paramref name="fact"/> added to the assumptions.</summary>
    public MathContext Assume(Expr fact) => this with { Assumptions = Assumptions.Add(fact) };

    /// <summary>Decides a proposition from the assumptions (see <see cref="AssumptionSet.Ask"/>).</summary>
    public Truth Ask(Expr proposition) => Assumptions.Ask(proposition);
}
