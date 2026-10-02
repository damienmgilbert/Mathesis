using System.Collections.Immutable;
using Mathesis.Numbers;

namespace Mathesis.Symbolics.Assumptions;

/// <summary>
/// A set of facts about symbols (<c>x &gt; 0</c>, <c>n in Z</c>, <c>a != 0</c>, <c>x in [0, pi]</c>, <c>x &gt; y</c>) and the questions
/// that can be asked of it (docs/design/04-type-system.md, "Truth and assumptions"; docs/design/07-engines.md, "Assumptions and domains").
/// </summary>
/// <remarks>
/// Propositions are judged at the points where every subexpression is defined: <see cref="Ask"/> returns
/// <see cref="Truth.True"/> only if the proposition holds at all such points that satisfy the facts, <see cref="Truth.False"/> only
/// if it fails at all of them, and <see cref="Truth.Unknown"/> otherwise. It never guesses.
/// </remarks>
public sealed record AssumptionSet
{
    private readonly Lazy<AssumptionAnalysis> _analysis;

    private AssumptionSet(ImmutableArray<Expr> facts)
    {
        Facts = facts;
        _analysis = new Lazy<AssumptionAnalysis>(() => new AssumptionAnalysis(facts));
    }

    /// <summary>No facts: every symbol is as general as its sort allows.</summary>
    public static AssumptionSet Empty { get; } = new([]);

    /// <summary>The facts, in the order they were added.</summary>
    public ImmutableArray<Expr> Facts { get; }

    /// <summary>Adds a fact (a Boolean expression; conjunctions are split, only facts of a recognized shape are used).</summary>
    /// <exception cref="ArgumentException"><paramref name="fact"/> is not a Boolean expression.</exception>
    public AssumptionSet Add(Expr fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        if (fact.Sort != Sort.Boolean) throw new ArgumentException($"The fact '{fact}' is not a Boolean expression.", nameof(fact));
        return Facts.Contains(fact) ? this : new AssumptionSet(Facts.Add(fact));
    }

    /// <summary>Adds several facts.</summary>
    public AssumptionSet Add(IEnumerable<Expr> facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        return facts.Aggregate(this, (set, fact) => set.Add(fact));
    }

    /// <summary>Decides a proposition from the facts (see the remarks on this type).</summary>
    public Truth Ask(Expr proposition)
    {
        ArgumentNullException.ThrowIfNull(proposition);
        return new AssumptionQuery(_analysis.Value).Ask(proposition);
    }

    /// <summary>The numeric interval the facts and the symbol's sort confine <paramref name="symbol"/> to, or <c>null</c> when it is unbounded.</summary>
    public Interval<double>? Bounds(Symbol symbol)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        var bounds = _analysis.Value.Bounds(symbol);
        return bounds.IsEntire ? null : bounds;
    }

    /// <summary>The signs <paramref name="e"/> can have at the points where it is defined.</summary>
    public SignInfo Sign(Expr e)
    {
        ArgumentNullException.ThrowIfNull(e);
        return new AssumptionQuery(_analysis.Value).Sign(e);
    }

    /// <summary>An interval that contains the value of <paramref name="e"/> at every point where it is defined and the facts hold.</summary>
    public Interval<double> Enclose(Expr e)
    {
        ArgumentNullException.ThrowIfNull(e);
        return new AssumptionQuery(_analysis.Value).Enclose(e);
    }

    /// <inheritdoc />
    public bool Equals(AssumptionSet? other) => other is not null && Facts.AsSpan().SequenceEqual(other.Facts.AsSpan());

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var fact in Facts) hash.Add(fact);
        return hash.ToHashCode();
    }
}
